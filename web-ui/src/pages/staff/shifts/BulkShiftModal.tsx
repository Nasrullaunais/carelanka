import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  createShiftsBulkMutation,
  listSkillsOptions,
} from '../../../services/api/generated/@tanstack/react-query.gen';
import type { BulkShiftPatternItem, BulkShiftRequest, StaffRole } from '../../../services/api/generated';
import { localDay } from '../../../types/datetime';
import { staffRoleLabels, staffRoles } from '../../../types/staff';
import { ModalDialog } from './ModalDialog';
import { WEEKDAYS } from './constants';

export function BulkShiftModal({
  wards,
  onClose,
  onSuccess,
}: {
  wards: Array<{ id: string; name: string }>;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [wardId, setWardId] = useState(wards[0]?.id ?? '');
  const [from, setFrom] = useState(() => localDay(new Date()));
  const [to, setTo] = useState(() => {
    const nextWeek = new Date();
    nextWeek.setDate(nextWeek.getDate() + 6);
    return localDay(nextWeek);
  });
  const [selectedWeekdays, setSelectedWeekdays] = useState<string[]>([
    'mon', 'tue', 'wed', 'thu', 'fri',
  ]);
  const [patterns, setPatterns] = useState<BulkShiftPatternItem[]>([
    { start_time: '08:00', end_time: '16:00', required_role: 'ward_nurse', headcount_needed: 2, minimum_headcount: 1, required_skill_id: null },
    { start_time: '16:00', end_time: '00:00', required_role: 'ward_nurse', headcount_needed: 2, minimum_headcount: 1, required_skill_id: null },
  ]);
  const [errorText, setErrorText] = useState('');

  const skillsQuery = useQuery(listSkillsOptions());
  const skills = skillsQuery.data ?? [];

  const bulkMutation = useMutation({
    ...createShiftsBulkMutation(),
    onSuccess: (res) => {
      toast.success(
        `Bulk shift planning complete: ${res.created} shifts created (${res.skipped} existing skipped)`
      );
      onSuccess();
    },
    onError: (err) => {
      const msg =
        (err as { detail?: string; message?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Bulk shift planning failed';
      toast.error(msg);
      setErrorText(msg);
    },
  });

  function toggleWeekday(code: string) {
    setSelectedWeekdays((prev) =>
      prev.includes(code) ? prev.filter((d) => d !== code) : [...prev, code]
    );
  }

  function handleAddPattern() {
    setPatterns((prev) => [
      ...prev,
      { start_time: '00:00', end_time: '08:00', required_role: 'ward_nurse', headcount_needed: 1, minimum_headcount: 1, required_skill_id: null },
    ]);
  }

  function handleRemovePattern(index: number) {
    if (patterns.length <= 1) {
      toast.error('At least one shift pattern is required');
      return;
    }
    setPatterns((prev) => prev.filter((_, i) => i !== index));
  }

  function updatePattern(index: number, patch: Partial<BulkShiftPatternItem>) {
    setPatterns((prev) =>
      prev.map((item, i) => (i === index ? { ...item, ...patch } : item))
    );
  }

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setErrorText('');

    if (!wardId) { setErrorText('Ward is required'); return; }
    if (!from || !to) { setErrorText('Both from and to dates are required'); return; }
    if (from > to) { setErrorText('From date cannot be after to date'); return; }
    if (selectedWeekdays.length === 0) { setErrorText('Select at least one weekday'); return; }
    if (patterns.length === 0) { setErrorText('At least one shift pattern is required'); return; }

    for (let i = 0; i < patterns.length; i++) {
      const p = patterns[i];
      if (!p.start_time || !p.end_time) {
        setErrorText(`Pattern #${i + 1} has empty start or end time`); return;
      }
      if (p.start_time === p.end_time) {
        setErrorText(`Pattern #${i + 1} start and end time cannot be identical`); return;
      }
      if (p.headcount_needed < 1) {
        setErrorText(`Pattern #${i + 1} headcount needed must be at least 1`); return;
      }
      if ((p.minimum_headcount ?? 1) > p.headcount_needed) {
        setErrorText(`Pattern #${i + 1} minimum headcount cannot exceed headcount needed`); return;
      }
    }

    const body: BulkShiftRequest = { ward_id: wardId, from, to, weekdays: selectedWeekdays, patterns };
    bulkMutation.mutate({ body });
  }

  return (
    <ModalDialog title="Bulk plan shifts" onClose={onClose} maxWidth="42rem">
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem' }}>
        <div>
          <label htmlFor="bulk-shift-ward">Target ward *</label>
          <select
            id="bulk-shift-ward"
            value={wardId}
            onChange={(e) => setWardId(e.target.value)}
            required
          >
            {wards.map((w) => (
              <option key={w.id} value={w.id}>{w.name}</option>
            ))}
          </select>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
          <div>
            <label htmlFor="bulk-shift-from">From date *</label>
            <input id="bulk-shift-from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} required />
          </div>
          <div>
            <label htmlFor="bulk-shift-to">To date *</label>
            <input id="bulk-shift-to" type="date" value={to} onChange={(e) => setTo(e.target.value)} required />
          </div>
        </div>

        <div>
          <label style={{ fontSize: '0.75rem', marginBottom: '0.35rem', display: 'block' }}>
            Active days of the week *
          </label>
          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            {WEEKDAYS.map(({ code, label }) => {
              const active = selectedWeekdays.includes(code);
              return (
                <button
                  key={code}
                  type="button"
                  className={active ? 'small' : 'secondary small'}
                  onClick={() => toggleWeekday(code)}
                  style={{ minWidth: '3.2rem', textAlign: 'center' }}
                >
                  {label}
                </button>
              );
            })}
          </div>
        </div>

        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.4rem' }}>
            <label style={{ margin: 0 }}>Shift daily patterns ({patterns.length}) *</label>
            <button type="button" className="secondary small" onClick={handleAddPattern}>
              + Add pattern
            </button>
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.6rem' }}>
            {patterns.map((pat, idx) => (
              <div
                key={idx}
                className="card"
                style={{ padding: '0.65rem 0.75rem', border: '1px solid var(--line)' }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.4rem' }}>
                  <strong style={{ fontSize: '0.85rem' }}>Pattern #{idx + 1}</strong>
                  {patterns.length > 1 && (
                    <button
                      type="button"
                      className="secondary small"
                      onClick={() => handleRemovePattern(idx)}
                      style={{ color: 'var(--danger, #b91c1c)' }}
                    >
                      Remove
                    </button>
                  )}
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(8.5rem, 1fr))', gap: '0.5rem' }}>
                  <div>
                    <label style={{ fontSize: '0.7rem' }}>Start time</label>
                    <input type="time" value={pat.start_time} onChange={(e) => updatePattern(idx, { start_time: e.target.value })} required />
                  </div>
                  <div>
                    <label style={{ fontSize: '0.7rem' }}>End time</label>
                    <input type="time" value={pat.end_time} onChange={(e) => updatePattern(idx, { end_time: e.target.value })} required />
                  </div>
                  <div>
                    <label style={{ fontSize: '0.7rem' }}>Role</label>
                    <select value={pat.required_role} onChange={(e) => updatePattern(idx, { required_role: e.target.value as StaffRole })} required>
                      {staffRoles.map((r) => (
                        <option key={r} value={r}>{staffRoleLabels[r]}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label style={{ fontSize: '0.7rem' }}>Skill (optional)</label>
                    <select value={pat.required_skill_id ?? ''} onChange={(e) => updatePattern(idx, { required_skill_id: e.target.value || null })}>
                      <option value="">None</option>
                      {skills.map((s) => (
                        <option key={s.id} value={s.id}>{s.name}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label style={{ fontSize: '0.7rem' }}>Headcount</label>
                    <input type="number" min="1" max="30" value={pat.headcount_needed} onChange={(e) => updatePattern(idx, { headcount_needed: Number(e.target.value) })} required />
                  </div>
                  <div>
                    <label style={{ fontSize: '0.7rem' }}>Minimum</label>
                    <input type="number" min="1" max={pat.headcount_needed} value={pat.minimum_headcount ?? 1} onChange={(e) => updatePattern(idx, { minimum_headcount: Number(e.target.value) })} required />
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>

        {errorText && (
          <p className="field-error" style={{ color: 'var(--danger, #b91c1c)', fontSize: '0.85rem' }}>
            {errorText}
          </p>
        )}

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
          <button type="button" className="secondary" onClick={onClose} disabled={bulkMutation.isPending}>
            Cancel
          </button>
          <button type="submit" disabled={bulkMutation.isPending}>
            {bulkMutation.isPending ? 'Generating shifts…' : 'Generate shifts'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}
