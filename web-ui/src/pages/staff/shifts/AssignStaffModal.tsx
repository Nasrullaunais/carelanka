import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  createAllocationMutation,
  listStaffOptions,
} from '../../../services/api/generated/@tanstack/react-query.gen';
import type { CreateAllocationRequest, ShiftDetailDto } from '../../../services/api/generated';
import { staffRoleLabels } from '../../../types/staff';
import { ModalDialog } from './ModalDialog';

export function AssignStaffModal({
  shift,
  onClose,
  onSuccess,
}: {
  shift: ShiftDetailDto;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [selectedStaffId, setSelectedStaffId] = useState('');
  const [matchRoleOnly, setMatchRoleOnly] = useState(true);
  const [search, setSearch] = useState('');
  const [isOverride, setIsOverride] = useState(false);
  const [overrideReason, setOverrideReason] = useState('');
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const staffQuery = useQuery({
    ...listStaffOptions({
      query: {
        includeInactive: false,
        pageSize: 100,
        ...(matchRoleOnly ? { role: shift.required_role } : {}),
        ...(search.trim() ? { search: search.trim() } : {}),
      },
    }),
  });

  const staffList = staffQuery.data?.items ?? [];

  const createMutation = useMutation({
    ...createAllocationMutation(),
    onSuccess: (data) => {
      toast.success(`Staff member ${data.staff_name} allocated to shift.`);
      onSuccess();
    },
    onError: (err) => {
      const msg =
        (err as { detail?: string; message?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Failed to allocate staff member. Check for schedule conflicts or qualification requirements.';
      toast.error(msg);
    },
  });

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const errors: Record<string, string> = {};
    if (!selectedStaffId) errors.staffId = 'Please select a staff member to allocate.';
    if (isOverride && !overrideReason.trim()) errors.overrideReason = 'An override reason is required when override is enabled.';

    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }
    setFieldErrors({});

    const body: CreateAllocationRequest = {
      shift_id: shift.id,
      staff_member_id: selectedStaffId,
      override: isOverride,
      override_reason: isOverride ? overrideReason.trim() : null,
    };

    createMutation.mutate({ body });
  }

  const selectedStaffMember = staffList.find((s) => s.id === selectedStaffId);

  return (
    <ModalDialog title={`Assign staff: ${shift.ward_name}`} onClose={onClose} maxWidth="36rem">
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem' }}>
        {/* Shift context card */}
        <div
          style={{
            padding: '0.6rem 0.85rem',
            background: 'var(--surface-sunken, #f8fafc)',
            borderRadius: '0.375rem',
            fontSize: '0.85rem',
            border: '1px solid var(--line)',
          }}
        >
          <div>
            <strong>Shift date &amp; time:</strong> {shift.date} ({shift.start_time} – {shift.end_time})
          </div>
          <div>
            <strong>Required role:</strong> {staffRoleLabels[shift.required_role] ?? shift.required_role}
            {shift.required_skill_name && ` · Skill: ${shift.required_skill_name}`}
          </div>
        </div>

        {/* Staff search & filter controls */}
        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', alignItems: 'flex-end' }}>
          <div style={{ flex: 1, minWidth: '12rem' }}>
            <label htmlFor="assign-staff-search" style={{ fontSize: '0.75rem' }}>
              Search staff by name or email
            </label>
            <input
              id="assign-staff-search"
              type="text"
              placeholder="e.g. Perera, Jayasuriya…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          <label
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '0.4rem',
              fontSize: '0.8rem',
              cursor: 'pointer',
              marginBottom: '0.35rem',
            }}
          >
            <input
              type="checkbox"
              checked={matchRoleOnly}
              onChange={(e) => setMatchRoleOnly(e.target.checked)}
            />
            {staffRoleLabels[shift.required_role]} only
          </label>
        </div>

        {/* Staff member select */}
        <div>
          <label htmlFor="assign-staff-select">Select staff member *</label>
          {staffQuery.isPending ? (
            <p className="muted" style={{ fontSize: '0.85rem' }}>Loading staff directory…</p>
          ) : staffList.length === 0 ? (
            <p className="empty" style={{ fontSize: '0.85rem', margin: '0.4rem 0' }}>
              No available staff found matching search/role criteria.
            </p>
          ) : (
            <select
              id="assign-staff-select"
              value={selectedStaffId}
              onChange={(e) => setSelectedStaffId(e.target.value)}
              required
            >
              <option value="">— Select an eligible staff member —</option>
              {staffList.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.full_name} ({staffRoleLabels[s.role] ?? s.role})
                  {s.department ? ` · ${s.department}` : ''}
                </option>
              ))}
            </select>
          )}
          {fieldErrors.staffId && (
            <p className="field-error" style={{ color: 'var(--danger, #b91c1c)', fontSize: '0.8rem' }}>
              {fieldErrors.staffId}
            </p>
          )}
        </div>

        {/* Selected staff detail preview */}
        {selectedStaffMember && (
          <div
            style={{
              padding: '0.5rem 0.75rem',
              background: 'rgba(37, 99, 235, 0.06)',
              borderRadius: '0.375rem',
              fontSize: '0.85rem',
              border: '1px solid rgba(37, 99, 235, 0.2)',
            }}
          >
            <strong>{selectedStaffMember.full_name}</strong> · Role: {staffRoleLabels[selectedStaffMember.role]}
            {selectedStaffMember.department && ` · Dept: ${selectedStaffMember.department}`}
          </div>
        )}

        {/* Override checkbox & reason */}
        <div style={{ borderTop: '1px solid var(--line)', paddingTop: '0.6rem' }}>
          <label style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', fontSize: '0.85rem', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={isOverride}
              onChange={(e) => setIsOverride(e.target.checked)}
            />
            <span>Override skill or role qualification requirements</span>
          </label>

          {isOverride && (
            <div style={{ marginTop: '0.5rem' }}>
              <label htmlFor="assign-override-reason" style={{ fontSize: '0.75rem' }}>
                Override rationale * (required by administrator policy)
              </label>
              <input
                id="assign-override-reason"
                type="text"
                placeholder="e.g. Critical ICU surge, staff member cross-trained under supervision"
                value={overrideReason}
                onChange={(e) => setOverrideReason(e.target.value)}
                maxLength={500}
                required={isOverride}
              />
              {fieldErrors.overrideReason && (
                <p className="field-error" style={{ color: 'var(--danger, #b91c1c)', fontSize: '0.8rem' }}>
                  {fieldErrors.overrideReason}
                </p>
              )}
            </div>
          )}
        </div>

        {/* Actions */}
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
          <button type="button" className="secondary" onClick={onClose} disabled={createMutation.isPending}>
            Cancel
          </button>
          <button type="submit" disabled={createMutation.isPending || !selectedStaffId}>
            {createMutation.isPending ? 'Assigning…' : 'Confirm allocation'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}
