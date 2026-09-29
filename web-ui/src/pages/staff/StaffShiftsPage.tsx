import { useState } from 'react';
import type { FormEvent, ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  cancelShiftMutation,
  createAllocationMutation,
  createShiftMutation,
  createShiftsBulkMutation,
  endAllocationMutation,
  getShiftOptions,
  listAllocationsOptions,
  listShiftsOptions,
  listSkillsOptions,
  listStaffOptions,
  listWardsOptions,
  updateShiftMutation,
} from '../../services/api/generated/@tanstack/react-query.gen';
import type {
  AllocationDto,
  AllocationEndReason,
  AllocationSource,
  AllocationStatus,
  BulkShiftPatternItem,
  BulkShiftRequest,
  CoverageStatus,
  CreateAllocationRequest,
  CreateShiftRequest,
  EndAllocationRequest,
  ShiftDetailDto,
  ShiftSummaryDto,
  StaffRole,
} from '../../services/api/generated';
import { useSession } from '../../services/auth/useSession';
import { localDateTime, localDay } from '../../types/datetime';
import { canManageStaff, canViewStaff } from '../../types/permissions';
import { coverageStatusLabels, coverageStatusTones, staffRoleLabels, staffRoles } from '../../types/staff';

const PAGE_SIZE = 15;

const WEEKDAYS = [
  { code: 'mon', label: 'Mon' },
  { code: 'tue', label: 'Tue' },
  { code: 'wed', label: 'Wed' },
  { code: 'thu', label: 'Thu' },
  { code: 'fri', label: 'Fri' },
  { code: 'sat', label: 'Sat' },
  { code: 'sun', label: 'Sun' },
];

export function StaffShiftsPage() {
  const session = useSession();
  const queryClient = useQueryClient();
  const role = session?.principal.role;

  const canView = canViewStaff(role);
  const canManage = canManageStaff(role);

  // Filters state
  const [selectedWardId, setSelectedWardId] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [selectedRole, setSelectedRole] = useState<StaffRole | ''>('');
  const [selectedCoverageStatus, setSelectedCoverageStatus] = useState<CoverageStatus | ''>('');
  const [page, setPage] = useState(1);

  // Modals state
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showBulkModal, setShowBulkModal] = useState(false);
  const [detailShiftId, setDetailShiftId] = useState<string | null>(null);
  const [editShift, setEditShift] = useState<ShiftDetailDto | ShiftSummaryDto | null>(null);
  const [cancelShiftTarget, setCancelShiftTarget] = useState<ShiftSummaryDto | ShiftDetailDto | null>(null);

  // Queries
  const wardsQuery = useQuery({
    ...listWardsOptions({ query: { isActive: true } }),
    enabled: canView,
  });

  const shiftsQuery = useQuery({
    ...listShiftsOptions({
      query: {
        ...(selectedWardId ? { wardId: selectedWardId } : {}),
        ...(fromDate ? { from: fromDate } : {}),
        ...(toDate ? { to: toDate } : {}),
        ...(selectedRole ? { role: selectedRole } : {}),
        ...(selectedCoverageStatus ? { coverageStatus: selectedCoverageStatus } : {}),
        page,
        pageSize: PAGE_SIZE,
      },
    }),
    enabled: canView,
  });

  const invalidateShiftQueries = () => {
    void queryClient.invalidateQueries({
      predicate: (query) => {
        const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;
        return (
          id === 'listShifts' ||
          id === 'getShift' ||
          id === 'listAllocations' ||
          id === 'getWardCoverage' ||
          id === 'getStaffMember'
        );
      },
    });
  };

  if (!canView) {
    return (
      <>
        <h1>Staff shifts & roster</h1>
        <p className="empty">
          Your role cannot view staff shifts. Hospital administrators and duty managers have access.
        </p>
      </>
    );
  }

  function handleResetFilters() {
    setSelectedWardId('');
    setFromDate('');
    setToDate('');
    setSelectedRole('');
    setSelectedCoverageStatus('');
    setPage(1);
  }

  const wards = wardsQuery.data ?? [];
  const paged = shiftsQuery.data;
  const shiftsList = paged?.items ?? [];
  const totalPages = paged?.total_pages ?? 1;

  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '1rem', marginBottom: '0.5rem' }}>
        <div>
          <h1>Staff shifts & roster</h1>
          <p className="muted">
            Ward shift scheduling, bulk shift planning, headcount requirements, and coverage tracking.
            {!canManage && ' (Duty Manager: view-only mode)'}
          </p>
        </div>

        <div className="actions">
          <Link to="/staff" className="secondary button" style={{ textDecoration: 'none' }}>
            Staff directory
          </Link>
          <Link to="/staff/coverage" className="secondary button" style={{ textDecoration: 'none' }}>
            Ward coverage
          </Link>
          <Link to="/staff/leave-approval" className="secondary button" style={{ textDecoration: 'none' }}>
            Leave requests
          </Link>
          <Link to="/staff/roster-proposals" className="secondary button" style={{ textDecoration: 'none' }}>
            Roster proposals
          </Link>

          {canManage && (
            <>
              <button
                type="button"
                className="secondary"
                onClick={() => setShowBulkModal(true)}
              >
                Bulk plan shifts
              </button>
              <button
                type="button"
                onClick={() => setShowCreateModal(true)}
              >
                + Add shift
              </button>
            </>
          )}
        </div>
      </div>

      {/* Filter Card */}
      <div className="card" style={{ marginBottom: '1rem' }}>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.75rem', alignItems: 'flex-end' }}>
          <div style={{ minWidth: '11rem', flex: 1 }}>
            <label htmlFor="shift-filter-ward" style={{ fontSize: '0.75rem' }}>
              Ward
            </label>
            <select
              id="shift-filter-ward"
              value={selectedWardId}
              onChange={(e) => {
                setSelectedWardId(e.target.value);
                setPage(1);
              }}
            >
              <option value="">All wards</option>
              {wards.map((w) => (
                <option key={w.id} value={w.id}>
                  {w.name}
                </option>
              ))}
            </select>
          </div>

          <div style={{ minWidth: '9rem' }}>
            <label htmlFor="shift-filter-from" style={{ fontSize: '0.75rem' }}>
              From date
            </label>
            <input
              id="shift-filter-from"
              type="date"
              value={fromDate}
              onChange={(e) => {
                setFromDate(e.target.value);
                setPage(1);
              }}
            />
          </div>

          <div style={{ minWidth: '9rem' }}>
            <label htmlFor="shift-filter-to" style={{ fontSize: '0.75rem' }}>
              To date
            </label>
            <input
              id="shift-filter-to"
              type="date"
              value={toDate}
              onChange={(e) => {
                setToDate(e.target.value);
                setPage(1);
              }}
            />
          </div>

          <div style={{ minWidth: '10rem' }}>
            <label htmlFor="shift-filter-role" style={{ fontSize: '0.75rem' }}>
              Required role
            </label>
            <select
              id="shift-filter-role"
              value={selectedRole}
              onChange={(e) => {
                setSelectedRole(e.target.value as StaffRole | '');
                setPage(1);
              }}
            >
              <option value="">All roles</option>
              {staffRoles.map((r) => (
                <option key={r} value={r}>
                  {staffRoleLabels[r]}
                </option>
              ))}
            </select>
          </div>

          <div style={{ minWidth: '10rem' }}>
            <label htmlFor="shift-filter-status" style={{ fontSize: '0.75rem' }}>
              Coverage status
            </label>
            <select
              id="shift-filter-status"
              value={selectedCoverageStatus}
              onChange={(e) => {
                setSelectedCoverageStatus(e.target.value as CoverageStatus | '');
                setPage(1);
              }}
            >
              <option value="">All statuses</option>
              {(Object.keys(coverageStatusLabels) as CoverageStatus[]).map((st) => (
                <option key={st} value={st}>
                  {coverageStatusLabels[st]}
                </option>
              ))}
            </select>
          </div>

          {(selectedWardId || fromDate || toDate || selectedRole || selectedCoverageStatus) && (
            <button
              type="button"
              className="secondary small"
              onClick={handleResetFilters}
              style={{ alignSelf: 'flex-end', marginBottom: '0.2rem' }}
            >
              Clear filters
            </button>
          )}
        </div>
      </div>

      {/* Shifts Table */}
      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem', flexWrap: 'wrap', gap: '0.5rem' }}>
          <h2>
            Scheduled shifts {paged?.total_items !== undefined ? `(${paged.total_items})` : ''}
          </h2>
          {shiftsQuery.isFetching && <span className="muted" style={{ fontSize: '0.85rem' }}>Refreshing shifts…</span>}
        </div>

        {shiftsQuery.isPending ? (
          <p className="muted">Loading scheduled shifts…</p>
        ) : shiftsQuery.isError ? (
          <p className="empty">
            Failed to load shifts:{' '}
            {shiftsQuery.error instanceof Error ? shiftsQuery.error.message : 'Unknown error'}
          </p>
        ) : shiftsList.length === 0 ? (
          <div className="empty" style={{ padding: '2rem 1rem', textAlign: 'center' }}>
            <p>No scheduled shifts found matching the selected filters.</p>
            {canManage && (
              <div style={{ marginTop: '0.75rem' }}>
                <button type="button" onClick={() => setShowCreateModal(true)}>
                  Create first shift
                </button>
              </div>
            )}
          </div>
        ) : (
          <>
            <div style={{ overflowX: 'auto' }}>
              <table>
                <thead>
                  <tr>
                    <th>Date</th>
                    <th>Ward</th>
                    <th>Time window</th>
                    <th>Role required</th>
                    <th>Required skill</th>
                    <th>Headcount</th>
                    <th>Coverage status</th>
                    <th style={{ textAlign: 'right' }}>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {shiftsList.map((shift) => {
                    const coverage = shift.coverage;
                    const statusTone = coverage ? coverageStatusTones[coverage.status] : 'badge';
                    const statusLabel = coverage ? coverageStatusLabels[coverage.status] : 'Unknown';

                    return (
                      <tr key={shift.id}>
                        <td>
                          <strong>{shift.date}</strong>
                        </td>
                        <td>{shift.ward_name}</td>
                        <td>
                          {shift.start_time} – {shift.end_time}
                          {shift.crosses_midnight && (
                            <span className="badge" style={{ marginLeft: '0.4rem', fontSize: '0.7rem' }}>
                              +1 day
                            </span>
                          )}
                        </td>
                        <td>{staffRoleLabels[shift.required_role] ?? shift.required_role}</td>
                        <td>
                          {shift.required_skill_name ? (
                            <span className="badge">{shift.required_skill_name}</span>
                          ) : (
                            <span className="muted">—</span>
                          )}
                        </td>
                        <td>
                          <strong>{coverage?.confirmed_count ?? 0}</strong> / {shift.headcount_needed}
                          <span className="muted" style={{ fontSize: '0.75rem', marginLeft: '0.35rem' }}>
                            (min {shift.minimum_headcount})
                          </span>
                        </td>
                        <td>
                          <span className={statusTone}>{statusLabel}</span>
                        </td>
                        <td style={{ textAlign: 'right' }}>
                          <div style={{ display: 'inline-flex', gap: '0.4rem', alignItems: 'center' }}>
                            <button
                              type="button"
                              className="secondary small"
                              onClick={() => setDetailShiftId(shift.id)}
                            >
                              View
                            </button>
                            {canManage && (
                              <>
                                <button
                                  type="button"
                                  className="secondary small"
                                  onClick={() => setEditShift(shift)}
                                >
                                  Edit
                                </button>
                                <button
                                  type="button"
                                  className="secondary small"
                                  style={{ color: 'var(--danger, #b91c1c)' }}
                                  onClick={() => setCancelShiftTarget(shift)}
                                >
                                  Cancel
                                </button>
                              </>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            {/* Pagination */}
            {totalPages > 1 && (
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '1rem', flexWrap: 'wrap', gap: '0.5rem' }}>
                <span className="muted" style={{ fontSize: '0.85rem' }}>
                  Page {page} of {totalPages} ({paged?.total_items ?? 0} shifts)
                </span>
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <button
                    type="button"
                    className="secondary small"
                    disabled={page <= 1}
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                  >
                    Previous
                  </button>
                  <button
                    type="button"
                    className="secondary small"
                    disabled={page >= totalPages}
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  >
                    Next
                  </button>
                </div>
              </div>
            )}
          </>
        )}
      </div>

      {/* Modal: Single Shift Creation */}
      {showCreateModal && (
        <CreateShiftModal
          wards={wards}
          onClose={() => setShowCreateModal(false)}
          onSuccess={() => {
            setShowCreateModal(false);
            invalidateShiftQueries();
          }}
        />
      )}

      {/* Modal: Bulk Shift Planning */}
      {showBulkModal && (
        <BulkShiftModal
          wards={wards}
          onClose={() => setShowBulkModal(false)}
          onSuccess={() => {
            setShowBulkModal(false);
            invalidateShiftQueries();
          }}
        />
      )}

      {/* Modal: Shift Detail */}
      {detailShiftId && (
        <ShiftDetailModal
          shiftId={detailShiftId}
          canManage={canManage}
          onClose={() => setDetailShiftId(null)}
          onEdit={(shift) => {
            setDetailShiftId(null);
            setEditShift(shift);
          }}
          onCancel={(shift) => {
            setDetailShiftId(null);
            setCancelShiftTarget(shift);
          }}
        />
      )}

      {/* Modal: Edit Shift */}
      {editShift && (
        <EditShiftModal
          shift={editShift}
          wards={wards}
          onClose={() => setEditShift(null)}
          onSuccess={() => {
            setEditShift(null);
            invalidateShiftQueries();
          }}
        />
      )}

      {/* Modal: Cancel Shift Confirmation */}
      {cancelShiftTarget && (
        <CancelShiftModal
          shift={cancelShiftTarget}
          onClose={() => setCancelShiftTarget(null)}
          onSuccess={() => {
            setCancelShiftTarget(null);
            invalidateShiftQueries();
          }}
        />
      )}
    </>
  );
}

// -------------------------------------------------------------
// Shared Modal Wrapper
// -------------------------------------------------------------

function ModalDialog({
  title,
  onClose,
  maxWidth = '34rem',
  children,
}: {
  title: string;
  onClose: () => void;
  maxWidth?: string;
  children: ReactNode;
}) {
  return (
    <div className="dialog-backdrop" role="dialog" aria-modal="true" aria-label={title}>
      <div className="dialog card" style={{ maxWidth }}>
        <div className="dialog-head">
          <h2>{title}</h2>
          <button type="button" className="secondary" onClick={onClose} aria-label="Close">
            ×
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}

// -------------------------------------------------------------
// Create Single Shift Modal
// -------------------------------------------------------------

function CreateShiftModal({
  wards,
  onClose,
  onSuccess,
}: {
  wards: Array<{ id: string; name: string }>;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [wardId, setWardId] = useState(wards[0]?.id ?? '');
  const [date, setDate] = useState(() => localDay(new Date()));
  const [startTime, setStartTime] = useState('08:00');
  const [endTime, setEndTime] = useState('16:00');
  const [requiredRole, setRequiredRole] = useState<StaffRole>('ward_nurse');
  const [requiredSkillId, setRequiredSkillId] = useState('');
  const [headcountNeeded, setHeadcountNeeded] = useState(2);
  const [minimumHeadcount, setMinimumHeadcount] = useState(1);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const skillsQuery = useQuery(listSkillsOptions());
  const skills = skillsQuery.data ?? [];

  const createMutation = useMutation({
    ...createShiftMutation(),
    onSuccess: () => {
      toast.success('Shift created successfully');
      onSuccess();
    },
    onError: (err) => {
      const msg = (err as { detail?: string; message?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Failed to create shift';
      toast.error(msg);
    },
  });

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const errors: Record<string, string> = {};

    if (!wardId) errors.wardId = 'Ward is required';
    if (!date) errors.date = 'Date is required';
    if (!startTime) errors.startTime = 'Start time is required';
    if (!endTime) errors.endTime = 'End time is required';
    if (startTime && endTime && startTime === endTime) {
      errors.endTime = 'Start time and end time cannot be identical';
    }
    if (headcountNeeded < 1) errors.headcountNeeded = 'Must be at least 1';
    if (minimumHeadcount < 1) errors.minimumHeadcount = 'Must be at least 1';
    if (minimumHeadcount > headcountNeeded) {
      errors.minimumHeadcount = 'Minimum headcount cannot exceed headcount needed';
    }

    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }
    setFieldErrors({});

    const body: CreateShiftRequest = {
      ward_id: wardId,
      date,
      start_time: startTime,
      end_time: endTime,
      required_role: requiredRole,
      required_skill_id: requiredSkillId || null,
      headcount_needed: Number(headcountNeeded),
      minimum_headcount: Number(minimumHeadcount),
    };

    createMutation.mutate({ body });
  }

  return (
    <ModalDialog title="Create single shift" onClose={onClose} maxWidth="36rem">
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem' }}>
        <div>
          <label htmlFor="create-shift-ward">Ward *</label>
          <select
            id="create-shift-ward"
            value={wardId}
            onChange={(e) => setWardId(e.target.value)}
            required
          >
            {wards.map((w) => (
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </select>
          {fieldErrors.wardId && <p className="field-error" style={{ color: 'var(--danger, #b91c1c)', fontSize: '0.8rem' }}>{fieldErrors.wardId}</p>}
        </div>

        <div>
          <label htmlFor="create-shift-date">Shift date *</label>
          <input
            id="create-shift-date"
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value)}
            required
          />
          {fieldErrors.date && <p className="field-error" style={{ color: 'var(--danger, #b91c1c)', fontSize: '0.8rem' }}>{fieldErrors.date}</p>}
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
          <div>
            <label htmlFor="create-shift-start">Start time (HH:mm) *</label>
            <input
              id="create-shift-start"
              type="time"
              value={startTime}
              onChange={(e) => setStartTime(e.target.value)}
              required
            />
            {fieldErrors.startTime && <p className="field-error" style={{ color: 'var(--danger, #b91c1c)', fontSize: '0.8rem' }}>{fieldErrors.startTime}</p>}
          </div>

          <div>
            <label htmlFor="create-shift-end">End time (HH:mm) *</label>
            <input
              id="create-shift-end"
              type="time"
              value={endTime}
              onChange={(e) => setEndTime(e.target.value)}
              required
            />
            {fieldErrors.endTime && <p className="field-error" style={{ color: 'var(--danger, #b91c1c)', fontSize: '0.8rem' }}>{fieldErrors.endTime}</p>}
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
          <div>
            <label htmlFor="create-shift-role">Required role *</label>
            <select
              id="create-shift-role"
              value={requiredRole}
              onChange={(e) => setRequiredRole(e.target.value as StaffRole)}
              required
            >
              {staffRoles.map((r) => (
                <option key={r} value={r}>
                  {staffRoleLabels[r]}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="create-shift-skill">Required skill (optional)</label>
            <select
              id="create-shift-skill"
              value={requiredSkillId}
              onChange={(e) => setRequiredSkillId(e.target.value)}
            >
              <option value="">None (any qualified {staffRoleLabels[requiredRole]})</option>
              {skills.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
          <div>
            <label htmlFor="create-shift-headcount">Headcount needed *</label>
            <input
              id="create-shift-headcount"
              type="number"
              min="1"
              max="50"
              value={headcountNeeded}
              onChange={(e) => setHeadcountNeeded(Number(e.target.value))}
              required
            />
            {fieldErrors.headcountNeeded && <p className="field-error" style={{ color: 'var(--danger, #b91c1c)', fontSize: '0.8rem' }}>{fieldErrors.headcountNeeded}</p>}
          </div>

          <div>
            <label htmlFor="create-shift-min-headcount">Minimum headcount *</label>
            <input
              id="create-shift-min-headcount"
              type="number"
              min="1"
              max={headcountNeeded}
              value={minimumHeadcount}
              onChange={(e) => setMinimumHeadcount(Number(e.target.value))}
              required
            />
            {fieldErrors.minimumHeadcount && <p className="field-error" style={{ color: 'var(--danger, #b91c1c)', fontSize: '0.8rem' }}>{fieldErrors.minimumHeadcount}</p>}
          </div>
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
          <button type="button" className="secondary" onClick={onClose} disabled={createMutation.isPending}>
            Cancel
          </button>
          <button type="submit" disabled={createMutation.isPending}>
            {createMutation.isPending ? 'Creating…' : 'Create shift'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}

// -------------------------------------------------------------
// Bulk Shift Planning Modal
// -------------------------------------------------------------

function BulkShiftModal({
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
    'mon',
    'tue',
    'wed',
    'thu',
    'fri',
  ]);
  const [patterns, setPatterns] = useState<BulkShiftPatternItem[]>([
    {
      start_time: '08:00',
      end_time: '16:00',
      required_role: 'ward_nurse',
      headcount_needed: 2,
      minimum_headcount: 1,
      required_skill_id: null,
    },
    {
      start_time: '16:00',
      end_time: '00:00',
      required_role: 'ward_nurse',
      headcount_needed: 2,
      minimum_headcount: 1,
      required_skill_id: null,
    },
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
      const msg = (err as { detail?: string; message?: string })?.detail ??
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
      {
        start_time: '00:00',
        end_time: '08:00',
        required_role: 'ward_nurse',
        headcount_needed: 1,
        minimum_headcount: 1,
        required_skill_id: null,
      },
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

    if (!wardId) {
      setErrorText('Ward is required');
      return;
    }
    if (!from || !to) {
      setErrorText('Both from and to dates are required');
      return;
    }
    if (from > to) {
      setErrorText('From date cannot be after to date');
      return;
    }
    if (selectedWeekdays.length === 0) {
      setErrorText('Select at least one weekday');
      return;
    }
    if (patterns.length === 0) {
      setErrorText('At least one shift pattern is required');
      return;
    }

    for (let i = 0; i < patterns.length; i++) {
      const p = patterns[i];
      if (!p.start_time || !p.end_time) {
        setErrorText(`Pattern #${i + 1} has empty start or end time`);
        return;
      }
      if (p.start_time === p.end_time) {
        setErrorText(`Pattern #${i + 1} start and end time cannot be identical`);
        return;
      }
      if (p.headcount_needed < 1) {
        setErrorText(`Pattern #${i + 1} headcount needed must be at least 1`);
        return;
      }
      if ((p.minimum_headcount ?? 1) > p.headcount_needed) {
        setErrorText(`Pattern #${i + 1} minimum headcount cannot exceed headcount needed`);
        return;
      }
    }

    const body: BulkShiftRequest = {
      ward_id: wardId,
      from,
      to,
      weekdays: selectedWeekdays,
      patterns,
    };

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
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </select>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
          <div>
            <label htmlFor="bulk-shift-from">From date *</label>
            <input
              id="bulk-shift-from"
              type="date"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
              required
            />
          </div>

          <div>
            <label htmlFor="bulk-shift-to">To date *</label>
            <input
              id="bulk-shift-to"
              type="date"
              value={to}
              onChange={(e) => setTo(e.target.value)}
              required
            />
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
                    <input
                      type="time"
                      value={pat.start_time}
                      onChange={(e) => updatePattern(idx, { start_time: e.target.value })}
                      required
                    />
                  </div>
                  <div>
                    <label style={{ fontSize: '0.7rem' }}>End time</label>
                    <input
                      type="time"
                      value={pat.end_time}
                      onChange={(e) => updatePattern(idx, { end_time: e.target.value })}
                      required
                    />
                  </div>
                  <div>
                    <label style={{ fontSize: '0.7rem' }}>Role</label>
                    <select
                      value={pat.required_role}
                      onChange={(e) => updatePattern(idx, { required_role: e.target.value as StaffRole })}
                      required
                    >
                      {staffRoles.map((r) => (
                        <option key={r} value={r}>
                          {staffRoleLabels[r]}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label style={{ fontSize: '0.7rem' }}>Skill (optional)</label>
                    <select
                      value={pat.required_skill_id ?? ''}
                      onChange={(e) => updatePattern(idx, { required_skill_id: e.target.value || null })}
                    >
                      <option value="">None</option>
                      {skills.map((s) => (
                        <option key={s.id} value={s.id}>
                          {s.name}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label style={{ fontSize: '0.7rem' }}>Headcount</label>
                    <input
                      type="number"
                      min="1"
                      max="30"
                      value={pat.headcount_needed}
                      onChange={(e) => updatePattern(idx, { headcount_needed: Number(e.target.value) })}
                      required
                    />
                  </div>
                  <div>
                    <label style={{ fontSize: '0.7rem' }}>Minimum</label>
                    <input
                      type="number"
                      min="1"
                      max={pat.headcount_needed}
                      value={pat.minimum_headcount ?? 1}
                      onChange={(e) => updatePattern(idx, { minimum_headcount: Number(e.target.value) })}
                      required
                    />
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

// -------------------------------------------------------------
// Allocation Status & Reason Constants
// -------------------------------------------------------------

const allocationStatusLabels: Record<AllocationStatus, string> = {
  confirmed: 'Confirmed',
  proposed: 'Proposed',
  released: 'Released',
  cancelled: 'Cancelled',
};

const allocationStatusTones: Record<AllocationStatus, string> = {
  confirmed: 'badge',
  proposed: 'badge severity-low',
  released: 'badge retired',
  cancelled: 'badge severity-critical',
};

const allocationSourceLabels: Record<AllocationSource, string> = {
  manual: 'Manual',
  agent_proposal: 'AI Proposal',
  swap_request: 'Swap request',
};

const allocationEndReasonLabels: Record<AllocationEndReason, string> = {
  manual: 'Manual reassignment',
  leave_approved: 'Leave approved',
  swapped_out: 'Swapped out',
  shift_cancelled: 'Shift cancelled',
  staff_deactivated: 'Staff deactivated',
};

const ALLOCATION_END_REASONS: Array<{ value: AllocationEndReason; label: string }> = [
  { value: 'manual', label: 'Manual reassignment' },
  { value: 'leave_approved', label: 'Leave approved' },
  { value: 'swapped_out', label: 'Swapped out' },
  { value: 'shift_cancelled', label: 'Shift cancelled' },
  { value: 'staff_deactivated', label: 'Staff deactivated' },
];

// -------------------------------------------------------------
// Shift Detail Modal (Group 1 + Group 2 Allocation Management)
// -------------------------------------------------------------

function ShiftDetailModal({
  shiftId,
  canManage,
  onClose,
  onEdit,
  onCancel,
}: {
  shiftId: string;
  canManage: boolean;
  onClose: () => void;
  onEdit: (shift: ShiftDetailDto) => void;
  onCancel: (shift: ShiftDetailDto) => void;
}) {
  const queryClient = useQueryClient();
  const [showAssignModal, setShowAssignModal] = useState(false);
  const [endingAllocation, setEndingAllocation] = useState<AllocationDto | null>(null);
  const [showEndedAllocations, setShowEndedAllocations] = useState(true);

  const detailQuery = useQuery(getShiftOptions({ path: { id: shiftId } }));
  const shift = detailQuery.data;

  // Group 2: listAllocations endpoint integration
  const allocationsQuery = useQuery({
    ...listAllocationsOptions({ query: { shiftId } }),
    enabled: Boolean(shiftId),
  });

  const invalidateData = () => {
    void queryClient.invalidateQueries({
      predicate: (query) => {
        const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;
        return (
          id === 'listShifts' ||
          id === 'getShift' ||
          id === 'listAllocations' ||
          id === 'getWardCoverage' ||
          id === 'getStaffMember'
        );
      },
    });
    void allocationsQuery.refetch();
    void detailQuery.refetch();
  };

  if (detailQuery.isPending) {
    return (
      <ModalDialog title="Shift details" onClose={onClose} maxWidth="42rem">
        <p className="muted">Loading shift details…</p>
      </ModalDialog>
    );
  }

  if (detailQuery.isError || !shift) {
    return (
      <ModalDialog title="Shift details" onClose={onClose} maxWidth="42rem">
        <p className="empty">
          Failed to load shift:{' '}
          {detailQuery.error instanceof Error ? detailQuery.error.message : 'Not found'}
        </p>
      </ModalDialog>
    );
  }

  const coverage = shift.coverage;
  const statusTone = coverage ? coverageStatusTones[coverage.status] : 'badge';
  const statusLabel = coverage ? coverageStatusLabels[coverage.status] : 'Unknown';

  // Allocations from listAllocations query, falling back to shift.allocations
  const allAllocations = allocationsQuery.data?.items ?? shift.allocations ?? [];
  const activeAllocations = allAllocations.filter(
    (a) => !a.ended_at && a.status !== 'released' && a.status !== 'cancelled'
  );
  const endedAllocations = allAllocations.filter(
    (a) => Boolean(a.ended_at) || a.status === 'released' || a.status === 'cancelled'
  );

  const displayedAllocations = showEndedAllocations ? allAllocations : activeAllocations;

  return (
    <>
      <ModalDialog
        title={`${shift.ward_name} · ${shift.date}`}
        onClose={onClose}
        maxWidth="44rem"
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          {/* Timing banner */}
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem' }}>
            <div>
              <p style={{ margin: 0, fontSize: '1.1rem', fontWeight: 600 }}>
                {shift.start_time} – {shift.end_time}
                {shift.crosses_midnight && (
                  <span className="badge" style={{ marginLeft: '0.5rem', fontSize: '0.75rem' }}>
                    Crosses midnight (+1 day)
                  </span>
                )}
              </p>
              <p className="muted" style={{ margin: '0.2rem 0 0', fontSize: '0.85rem' }}>
                Shift ID: {shift.id}
              </p>
            </div>
            <span className={statusTone} style={{ fontSize: '0.85rem', padding: '0.25rem 0.6rem' }}>
              {statusLabel}
            </span>
          </div>

          {/* Shift Details Definition List */}
          <dl className="grid-list" style={{ margin: 0 }}>
            <div>
              <dt>Ward</dt>
              <dd><strong>{shift.ward_name}</strong></dd>
            </div>
            <div>
              <dt>Date</dt>
              <dd>{shift.date}</dd>
            </div>
            <div>
              <dt>Required role</dt>
              <dd>{staffRoleLabels[shift.required_role] ?? shift.required_role}</dd>
            </div>
            <div>
              <dt>Required qualification</dt>
              <dd>
                {shift.required_skill_name ? (
                  <span className="badge">{shift.required_skill_name}</span>
                ) : (
                  <span className="muted">None specified</span>
                )}
              </dd>
            </div>
            <div>
              <dt>Headcount needed</dt>
              <dd><strong>{shift.headcount_needed}</strong> staff</dd>
            </div>
            <div>
              <dt>Minimum headcount</dt>
              <dd><strong>{shift.minimum_headcount}</strong> staff</dd>
            </div>
            <div>
              <dt>Currently allocated</dt>
              <dd>
                <strong>{coverage?.confirmed_count ?? activeAllocations.length}</strong> confirmed
              </dd>
            </div>
            <div>
              <dt>Shortfall to minimum</dt>
              <dd>
                {coverage && coverage.shortfall_to_minimum > 0 ? (
                  <strong style={{ color: 'var(--danger, #b91c1c)' }}>
                    {coverage.shortfall_to_minimum} below minimum
                  </strong>
                ) : (
                  <span style={{ color: 'var(--accent, #15803d)' }}>None (covered)</span>
                )}
              </dd>
            </div>
          </dl>

          {/* Open proposal alert */}
          {shift.open_proposal_id && (
            <div
              className="card"
              style={{
                padding: '0.75rem 1rem',
                background: 'rgba(217, 119, 6, 0.08)',
                border: '1px solid rgba(217, 119, 6, 0.3)',
              }}
            >
              <strong style={{ color: 'var(--warning, #b45309)' }}>
                Active AI Roster Proposal
              </strong>
              <p className="muted" style={{ margin: '0.25rem 0 0.5rem', fontSize: '0.85rem' }}>
                The AI Roster Agent has raised a swap/fill proposal to resolve staffing for this shift.
              </p>
              <Link
                to="/staff/roster-proposals"
                className="button secondary small"
                style={{ textDecoration: 'none' }}
                onClick={onClose}
              >
                Review roster proposal →
              </Link>
            </div>
          )}

          {/* Group 2: Allocated Staff Section */}
          <div style={{ marginTop: '0.5rem', borderTop: '1px solid var(--line)', paddingTop: '0.85rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem', marginBottom: '0.6rem' }}>
              <div>
                <h3 style={{ margin: 0 }}>
                  Allocated Staff ({activeAllocations.length} active{endedAllocations.length > 0 ? `, ${endedAllocations.length} ended` : ''})
                </h3>
                <p className="muted" style={{ margin: '0.15rem 0 0', fontSize: '0.8rem' }}>
                  Staff assigned to cover this shift. Active allocations contribute to ward coverage.
                </p>
              </div>

              <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                {endedAllocations.length > 0 && (
                  <button
                    type="button"
                    className="secondary small"
                    onClick={() => setShowEndedAllocations((prev) => !prev)}
                    style={{ fontSize: '0.75rem' }}
                  >
                    {showEndedAllocations ? 'Hide ended history' : 'Show ended history'}
                  </button>
                )}
                {canManage && (
                  <button
                    type="button"
                    className="small"
                    onClick={() => setShowAssignModal(true)}
                  >
                    + Assign staff
                  </button>
                )}
              </div>
            </div>

            {allocationsQuery.isPending && (
              <p className="muted" style={{ fontSize: '0.85rem' }}>Loading allocated staff…</p>
            )}

            {allocationsQuery.isError && (
              <p className="empty" style={{ fontSize: '0.85rem' }}>
                Failed to load allocations:{' '}
                {allocationsQuery.error instanceof Error ? allocationsQuery.error.message : 'Error'}
              </p>
            )}

            {!allocationsQuery.isPending && displayedAllocations.length === 0 ? (
              <div
                style={{
                  padding: '1.25rem',
                  textAlign: 'center',
                  background: 'var(--surface-sunken, #f8fafc)',
                  borderRadius: '0.5rem',
                  border: '1px solid var(--line)',
                }}
              >
                <p className="muted" style={{ margin: 0, fontSize: '0.85rem' }}>
                  No staff members currently allocated to this shift.
                </p>
                {canManage && (
                  <button
                    type="button"
                    className="secondary small"
                    style={{ marginTop: '0.5rem' }}
                    onClick={() => setShowAssignModal(true)}
                  >
                    Assign staff member now
                  </button>
                )}
              </div>
            ) : (
              <div style={{ overflowX: 'auto' }}>
                <table>
                  <thead>
                    <tr>
                      <th>Staff member</th>
                      <th>Status</th>
                      <th>Source</th>
                      <th>Duty information</th>
                      {canManage && <th style={{ textAlign: 'right' }}>Actions</th>}
                    </tr>
                  </thead>
                  <tbody>
                    {displayedAllocations.map((alloc) => {
                      const isActive =
                        !alloc.ended_at &&
                        alloc.status !== 'released' &&
                        alloc.status !== 'cancelled';
                      const statusTone = allocationStatusTones[alloc.status] ?? 'badge';
                      const statusLabel = allocationStatusLabels[alloc.status] ?? alloc.status;
                      const sourceLabel = allocationSourceLabels[alloc.source] ?? alloc.source;

                      return (
                        <tr
                          key={alloc.id}
                          style={!isActive ? { opacity: 0.65, background: 'rgba(0, 0, 0, 0.015)' } : undefined}
                        >
                          <td>
                            <strong>{alloc.staff_name}</strong>
                            <div className="muted" style={{ fontSize: '0.75rem' }}>
                              Allocated: {localDateTime(alloc.created_at)}
                            </div>
                          </td>
                          <td>
                            <span className={statusTone} style={{ fontSize: '0.75rem' }}>
                              {statusLabel}
                            </span>
                          </td>
                          <td>
                            <span className="badge" style={{ fontSize: '0.75rem' }}>
                              {sourceLabel}
                            </span>
                          </td>
                          <td style={{ fontSize: '0.8rem' }}>
                            {isActive ? (
                              <div>
                                <span style={{ color: 'var(--accent, #15803d)', fontWeight: 500 }}>
                                  ● Active on shift
                                </span>
                                {alloc.clocked_in_at && (
                                  <div className="muted" style={{ fontSize: '0.75rem' }}>
                                    Clocked in: {localDateTime(alloc.clocked_in_at)}
                                  </div>
                                )}
                              </div>
                            ) : (
                              <div>
                                <span style={{ color: 'var(--danger, #b91c1c)' }}>
                                  Ended {alloc.ended_at ? localDateTime(alloc.ended_at) : ''}
                                </span>
                                {alloc.ended_reason && (
                                  <div className="muted" style={{ fontSize: '0.75rem' }}>
                                    Reason: {allocationEndReasonLabels[alloc.ended_reason] ?? alloc.ended_reason}
                                  </div>
                                )}
                              </div>
                            )}
                          </td>
                          {canManage && (
                            <td style={{ textAlign: 'right' }}>
                              {isActive ? (
                                <button
                                  type="button"
                                  className="secondary small"
                                  style={{ color: 'var(--danger, #b91c1c)' }}
                                  onClick={() => setEndingAllocation(alloc)}
                                >
                                  End allocation
                                </button>
                              ) : (
                                <span className="muted" style={{ fontSize: '0.75rem' }}>
                                  Ended
                                </span>
                              )}
                            </td>
                          )}
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {/* Modal actions */}
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '0.5rem', paddingTop: '0.5rem', borderTop: '1px solid var(--line)' }}>
            <div>
              {canManage && (
                <button
                  type="button"
                  className="secondary"
                  style={{ color: 'var(--danger, #b91c1c)' }}
                  onClick={() => onCancel(shift)}
                >
                  Cancel shift
                </button>
              )}
            </div>
            <div style={{ display: 'flex', gap: '0.5rem' }}>
              {canManage && (
                <button type="button" className="secondary" onClick={() => onEdit(shift)}>
                  Edit shift
                </button>
              )}
              <button type="button" onClick={onClose}>
                Close
              </button>
            </div>
          </div>
        </div>
      </ModalDialog>

      {/* Modal: Assign Staff (Group 2) */}
      {showAssignModal && (
        <AssignStaffModal
          shift={shift}
          onClose={() => setShowAssignModal(false)}
          onSuccess={() => {
            setShowAssignModal(false);
            invalidateData();
          }}
        />
      )}

      {/* Modal: End Allocation (Group 2) */}
      {endingAllocation && (
        <EndAllocationModal
          allocation={endingAllocation}
          shift={shift}
          onClose={() => setEndingAllocation(null)}
          onSuccess={() => {
            setEndingAllocation(null);
            invalidateData();
          }}
        />
      )}
    </>
  );
}

// -------------------------------------------------------------
// Assign Staff to Shift Modal (Group 2: createAllocation)
// -------------------------------------------------------------

function AssignStaffModal({
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

  // Query staff directory
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

    if (!selectedStaffId) {
      errors.staffId = 'Please select a staff member to allocate.';
    }
    if (isOverride && !overrideReason.trim()) {
      errors.overrideReason = 'An override reason is required when override is enabled.';
    }

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
    <ModalDialog
      title={`Assign staff: ${shift.ward_name}`}
      onClose={onClose}
      maxWidth="36rem"
    >
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
            <strong>Shift date & time:</strong> {shift.date} ({shift.start_time} – {shift.end_time})
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
          <button
            type="button"
            className="secondary"
            onClick={onClose}
            disabled={createMutation.isPending}
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={createMutation.isPending || !selectedStaffId}
          >
            {createMutation.isPending ? 'Assigning…' : 'Confirm allocation'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}

// -------------------------------------------------------------
// End Allocation Modal (Group 2: endAllocation)
// -------------------------------------------------------------

function EndAllocationModal({
  allocation,
  shift,
  onClose,
  onSuccess,
}: {
  allocation: AllocationDto;
  shift: ShiftDetailDto;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [reason, setReason] = useState<AllocationEndReason>('manual');
  const [notes, setNotes] = useState('');
  const [suppressAgent, setSuppressAgent] = useState(false);

  const endMutation = useMutation({
    ...endAllocationMutation(),
    onSuccess: (res) => {
      toast.success(`Allocation for ${allocation.staff_name} ended.`);
      if (res.roster_proposal_id) {
        toast.info(
          `Ward coverage shortfall detected. Automatic AI Roster proposal triggered (ID: ${res.roster_proposal_id.slice(0, 8)}…).`,
          { duration: 6000 }
        );
      }
      onSuccess();
    },
    onError: (err) => {
      const msg =
        (err as { detail?: string; message?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Failed to end allocation';
      toast.error(msg);
    },
  });

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const body: EndAllocationRequest = {
      reason,
      notes: notes.trim() || null,
      suppress_agent: suppressAgent,
    };

    endMutation.mutate({
      path: { id: allocation.id },
      body,
    });
  }

  return (
    <ModalDialog
      title={`End allocation: ${allocation.staff_name}`}
      onClose={onClose}
      maxWidth="34rem"
    >
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem' }}>
        <p style={{ margin: 0, fontSize: '0.9rem' }}>
          Are you sure you want to end <strong>{allocation.staff_name}</strong>'s allocation on{' '}
          <strong>{shift.ward_name}</strong> ({shift.date}, {shift.start_time} – {shift.end_time})?
        </p>

        <div>
          <label htmlFor="end-allocation-reason">End reason *</label>
          <select
            id="end-allocation-reason"
            value={reason}
            onChange={(e) => setReason(e.target.value as AllocationEndReason)}
            required
          >
            {ALLOCATION_END_REASONS.map((r) => (
              <option key={r.value} value={r.value}>
                {r.label}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label htmlFor="end-allocation-notes">Notes (optional)</label>
          <textarea
            id="end-allocation-notes"
            rows={2}
            placeholder="Add relevant administrative notes or explanation…"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            maxLength={500}
          />
        </div>

        <div style={{ padding: '0.6rem 0.8rem', background: 'var(--surface-sunken, #f8fafc)', borderRadius: '0.375rem', border: '1px solid var(--line)' }}>
          <label style={{ display: 'flex', alignItems: 'flex-start', gap: '0.5rem', fontSize: '0.85rem', cursor: 'pointer', margin: 0 }}>
            <input
              type="checkbox"
              checked={suppressAgent}
              onChange={(e) => setSuppressAgent(e.target.checked)}
              style={{ marginTop: '0.2rem' }}
            />
            <div>
              <span>Suppress automatic AI Roster Agent proposal</span>
              <p className="muted" style={{ margin: '0.15rem 0 0', fontSize: '0.75rem' }}>
                If unchecked and ending this allocation drops ward headcount below minimum, the Staff Allocation
                Agent will automatically formulate a cascading swap proposal.
              </p>
            </div>
          </label>
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
          <button
            type="button"
            className="secondary"
            onClick={onClose}
            disabled={endMutation.isPending}
          >
            Cancel
          </button>
          <button
            type="submit"
            style={{ background: 'var(--danger, #b91c1c)', borderColor: 'var(--danger, #b91c1c)' }}
            disabled={endMutation.isPending}
          >
            {endMutation.isPending ? 'Ending allocation…' : 'End allocation'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}

// -------------------------------------------------------------
// Edit Shift Modal
// -------------------------------------------------------------

function EditShiftModal({
  shift,
  wards,
  onClose,
  onSuccess,
}: {
  shift: ShiftDetailDto | ShiftSummaryDto;
  wards: Array<{ id: string; name: string }>;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [wardId, setWardId] = useState(shift.ward_id);
  const [date, setDate] = useState(shift.date);
  const [startTime, setStartTime] = useState(shift.start_time);
  const [endTime, setEndTime] = useState(shift.end_time);
  const [requiredRole, setRequiredRole] = useState<StaffRole>(shift.required_role);
  const [requiredSkillId, setRequiredSkillId] = useState(shift.required_skill_id ?? '');
  const [headcountNeeded, setHeadcountNeeded] = useState(shift.headcount_needed);
  const [minimumHeadcount, setMinimumHeadcount] = useState(shift.minimum_headcount);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const skillsQuery = useQuery(listSkillsOptions());
  const skills = skillsQuery.data ?? [];

  const updateMutation = useMutation({
    ...updateShiftMutation(),
    onSuccess: () => {
      toast.success('Shift updated successfully');
      onSuccess();
    },
    onError: (err) => {
      const msg = (err as { detail?: string; message?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Failed to update shift';
      toast.error(msg);
    },
  });

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const errors: Record<string, string> = {};

    if (!wardId) errors.wardId = 'Ward is required';
    if (!date) errors.date = 'Date is required';
    if (!startTime) errors.startTime = 'Start time is required';
    if (!endTime) errors.endTime = 'End time is required';
    if (startTime && endTime && startTime === endTime) {
      errors.endTime = 'Start time and end time cannot be identical';
    }
    if (headcountNeeded < 1) errors.headcountNeeded = 'Must be at least 1';
    if (minimumHeadcount < 1) errors.minimumHeadcount = 'Must be at least 1';
    if (minimumHeadcount > headcountNeeded) {
      errors.minimumHeadcount = 'Minimum headcount cannot exceed headcount needed';
    }

    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }
    setFieldErrors({});

    const body: CreateShiftRequest = {
      ward_id: wardId,
      date,
      start_time: startTime,
      end_time: endTime,
      required_role: requiredRole,
      required_skill_id: requiredSkillId || null,
      headcount_needed: Number(headcountNeeded),
      minimum_headcount: Number(minimumHeadcount),
    };

    updateMutation.mutate({
      path: { id: shift.id },
      body,
    });
  }

  return (
    <ModalDialog title={`Edit shift: ${shift.ward_name} · ${shift.date}`} onClose={onClose} maxWidth="36rem">
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem' }}>
        <div>
          <label htmlFor="edit-shift-ward">Ward *</label>
          <select
            id="edit-shift-ward"
            value={wardId}
            onChange={(e) => setWardId(e.target.value)}
            required
          >
            {wards.map((w) => (
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </select>
          {fieldErrors.wardId && <p className="field-error" style={{ color: 'var(--danger, #b91c1c)', fontSize: '0.8rem' }}>{fieldErrors.wardId}</p>}
        </div>

        <div>
          <label htmlFor="edit-shift-date">Shift date *</label>
          <input
            id="edit-shift-date"
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value)}
            required
          />
          {fieldErrors.date && <p className="field-error" style={{ color: 'var(--danger, #b91c1c)', fontSize: '0.8rem' }}>{fieldErrors.date}</p>}
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
          <div>
            <label htmlFor="edit-shift-start">Start time (HH:mm) *</label>
            <input
              id="edit-shift-start"
              type="time"
              value={startTime}
              onChange={(e) => setStartTime(e.target.value)}
              required
            />
            {fieldErrors.startTime && <p className="field-error" style={{ color: 'var(--danger, #b91c1c)', fontSize: '0.8rem' }}>{fieldErrors.startTime}</p>}
          </div>

          <div>
            <label htmlFor="edit-shift-end">End time (HH:mm) *</label>
            <input
              id="edit-shift-end"
              type="time"
              value={endTime}
              onChange={(e) => setEndTime(e.target.value)}
              required
            />
            {fieldErrors.endTime && <p className="field-error" style={{ color: 'var(--danger, #b91c1c)', fontSize: '0.8rem' }}>{fieldErrors.endTime}</p>}
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
          <div>
            <label htmlFor="edit-shift-role">Required role *</label>
            <select
              id="edit-shift-role"
              value={requiredRole}
              onChange={(e) => setRequiredRole(e.target.value as StaffRole)}
              required
            >
              {staffRoles.map((r) => (
                <option key={r} value={r}>
                  {staffRoleLabels[r]}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="edit-shift-skill">Required skill (optional)</label>
            <select
              id="edit-shift-skill"
              value={requiredSkillId}
              onChange={(e) => setRequiredSkillId(e.target.value)}
            >
              <option value="">None (any qualified {staffRoleLabels[requiredRole]})</option>
              {skills.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
          <div>
            <label htmlFor="edit-shift-headcount">Headcount needed *</label>
            <input
              id="edit-shift-headcount"
              type="number"
              min="1"
              max="50"
              value={headcountNeeded}
              onChange={(e) => setHeadcountNeeded(Number(e.target.value))}
              required
            />
            {fieldErrors.headcountNeeded && <p className="field-error" style={{ color: 'var(--danger, #b91c1c)', fontSize: '0.8rem' }}>{fieldErrors.headcountNeeded}</p>}
          </div>

          <div>
            <label htmlFor="edit-shift-min-headcount">Minimum headcount *</label>
            <input
              id="edit-shift-min-headcount"
              type="number"
              min="1"
              max={headcountNeeded}
              value={minimumHeadcount}
              onChange={(e) => setMinimumHeadcount(Number(e.target.value))}
              required
            />
            {fieldErrors.minimumHeadcount && <p className="field-error" style={{ color: 'var(--danger, #b91c1c)', fontSize: '0.8rem' }}>{fieldErrors.minimumHeadcount}</p>}
          </div>
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
          <button type="button" className="secondary" onClick={onClose} disabled={updateMutation.isPending}>
            Cancel
          </button>
          <button type="submit" disabled={updateMutation.isPending}>
            {updateMutation.isPending ? 'Saving changes…' : 'Save changes'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}

// -------------------------------------------------------------
// Cancel Shift Confirmation Modal
// -------------------------------------------------------------

function CancelShiftModal({
  shift,
  onClose,
  onSuccess,
}: {
  shift: ShiftSummaryDto | ShiftDetailDto;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const cancelMutation = useMutation({
    ...cancelShiftMutation(),
    onSuccess: () => {
      toast.success('Shift cancelled successfully');
      onSuccess();
    },
    onError: (err) => {
      const msg = (err as { detail?: string; message?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Failed to cancel shift';
      toast.error(msg);
    },
  });

  function handleConfirm() {
    cancelMutation.mutate({ path: { id: shift.id } });
  }

  return (
    <ModalDialog
      title="Cancel scheduled shift"
      onClose={onClose}
      maxWidth="32rem"
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem' }}>
        <p>
          Are you sure you want to cancel the shift on <strong>{shift.date}</strong> for{' '}
          <strong>{shift.ward_name}</strong> ({shift.start_time} – {shift.end_time})?
        </p>

        <p className="muted" style={{ fontSize: '0.85rem' }}>
          Cancelling this shift removes the shift slot and automatically releases any staff allocations
          associated with it. This action cannot be undone.
        </p>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
          <button
            type="button"
            className="secondary"
            onClick={onClose}
            disabled={cancelMutation.isPending}
          >
            Keep shift
          </button>
          <button
            type="button"
            style={{ background: 'var(--danger, #b91c1c)', borderColor: 'var(--danger, #b91c1c)' }}
            onClick={handleConfirm}
            disabled={cancelMutation.isPending}
          >
            {cancelMutation.isPending ? 'Cancelling…' : 'Confirm cancellation'}
          </button>
        </div>
      </div>
    </ModalDialog>
  );
}
