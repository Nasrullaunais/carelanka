import { useState } from 'react';
import type { FormEvent, ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  decideLeaveRequestMutation,
  getLeaveRequestOptions,
  listLeaveRequestsOptions,
} from '../services/api/generated/@tanstack/react-query.gen';
import type {
  LeaveRequestDetailDto,
  LeaveRequestDto,
  LeaveStatus,
  LeaveType,
} from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { localDateTime } from '../types/datetime';
import { canManageLeave } from '../types/permissions';
import {
  coverageStatusLabels,
  coverageStatusTones,
  leaveStatusLabels,
  leaveStatusTones,
  leaveStatuses,
  leaveTypeLabels,
  leaveTypes,
} from '../types/staff';

const PAGE_SIZE = 15;

export function StaffLeaveApprovalPage() {
  const session = useSession();
  const queryClient = useQueryClient();
  const role = session?.principal.role;
  const currentUserId = session?.principal.id;

  const canManage = canManageLeave(role);

  // Filters state
  const [selectedStatus, setSelectedStatus] = useState<LeaveStatus | 'all'>('pending');
  const [selectedType, setSelectedType] = useState<LeaveType | 'all'>('all');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [page, setPage] = useState(1);

  // Modal / details state
  const [detailRequestId, setDetailRequestId] = useState<string | null>(null);
  const [decisionModal, setDecisionModal] = useState<{
    request: LeaveRequestDto | LeaveRequestDetailDto;
    action: 'approve' | 'reject';
  } | null>(null);

  // Queries
  const leaveQuery = useQuery({
    ...listLeaveRequestsOptions({
      query: {
        ...(selectedStatus !== 'all' ? { status: selectedStatus } : {}),
        ...(selectedType !== 'all' ? { type: selectedType } : {}),
        ...(fromDate ? { from: fromDate } : {}),
        ...(toDate ? { to: toDate } : {}),
        page,
        pageSize: PAGE_SIZE,
      },
    }),
    enabled: canManage,
  });

  const invalidateQueries = () => {
    void queryClient.invalidateQueries({
      predicate: (query) =>
        (query.queryKey[0] as { _id?: string } | undefined)?._id === 'listLeaveRequests',
    });
    if (detailRequestId) {
      void queryClient.invalidateQueries({
        predicate: (query) =>
          (query.queryKey[0] as { _id?: string } | undefined)?._id === 'getLeaveRequest',
      });
    }
  };

  if (!canManage) {
    return (
      <>
        <h1>Staff leave approval</h1>
        <p className="empty">
          Your role cannot review staff leave requests. Hospital administrators and duty managers have access.
        </p>
      </>
    );
  }

  function handleResetFilters() {
    setSelectedStatus('pending');
    setSelectedType('all');
    setFromDate('');
    setToDate('');
    setPage(1);
  }

  const paged = leaveQuery.data;
  const requests = paged?.items ?? [];
  const totalPages = paged?.total_pages ?? 1;

  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '1rem', marginBottom: '0.5rem' }}>
        <div>
          <h1>Staff leave approval</h1>
          <p className="muted">
            Review, approve, or reject employee leave and shift-swap requests.
          </p>
        </div>

        <div className="actions">
          <Link to="/staff" className="secondary button" style={{ textDecoration: 'none' }}>
            Staff directory
          </Link>
          <Link to="/staff/coverage" className="secondary button" style={{ textDecoration: 'none' }}>
            Ward coverage
          </Link>
          <button
            type="button"
            className="secondary"
            disabled={leaveQuery.isFetching}
            onClick={() => void leaveQuery.refetch()}
          >
            {leaveQuery.isFetching ? 'Refreshing…' : 'Refresh'}
          </button>
        </div>
      </div>

      {/* Filter Card */}
      <div className="card">
        <h2>Filter requests</h2>
        <div className="row">
          <div>
            <label htmlFor="leave-status-filter">Review status</label>
            <select
              id="leave-status-filter"
              value={selectedStatus}
              onChange={(e) => {
                setSelectedStatus(e.target.value as LeaveStatus | 'all');
                setPage(1);
              }}
            >
              <option value="all">All statuses</option>
              {leaveStatuses.map((s) => (
                <option key={s} value={s}>
                  {leaveStatusLabels[s]}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="leave-type-filter">Leave category</label>
            <select
              id="leave-type-filter"
              value={selectedType}
              onChange={(e) => {
                setSelectedType(e.target.value as LeaveType | 'all');
                setPage(1);
              }}
            >
              <option value="all">All leave types</option>
              {leaveTypes.map((t) => (
                <option key={t} value={t}>
                  {leaveTypeLabels[t]}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="leave-from-filter">From date</label>
            <input
              id="leave-from-filter"
              type="date"
              value={fromDate}
              onChange={(e) => {
                setFromDate(e.target.value);
                setPage(1);
              }}
            />
          </div>

          <div>
            <label htmlFor="leave-to-filter">To date</label>
            <input
              id="leave-to-filter"
              type="date"
              value={toDate}
              onChange={(e) => {
                setToDate(e.target.value);
                setPage(1);
              }}
            />
          </div>
        </div>

        {(selectedStatus !== 'pending' || selectedType !== 'all' || fromDate || toDate) && (
          <div className="actions" style={{ marginTop: '0.85rem' }}>
            <button type="button" className="secondary small" onClick={handleResetFilters}>
              Reset to pending
            </button>
          </div>
        )}
      </div>

      {/* Leave Requests Table */}
      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.75rem' }}>
          <h2>
            Leave requests {paged ? `(${paged.total_items})` : ''}
            {selectedStatus === 'pending' && ' — Awaiting decision'}
          </h2>
        </div>

        {leaveQuery.isLoading ? (
          <p className="empty">Loading leave requests…</p>
        ) : leaveQuery.isError ? (
          <div className="empty">
            <p style={{ color: 'var(--danger)' }}>Failed to load leave requests.</p>
            <button type="button" className="secondary" onClick={() => void leaveQuery.refetch()}>
              Retry
            </button>
          </div>
        ) : requests.length === 0 ? (
          <div className="empty">
            <p>No leave requests found matching the selected filters.</p>
            {selectedStatus === 'pending' && (
              <p className="muted" style={{ fontSize: '0.82rem' }}>
                All pending staff leave requests have been reviewed.
              </p>
            )}
          </div>
        ) : (
          <>
            <table>
              <thead>
                <tr>
                  <th>Staff member</th>
                  <th>Type</th>
                  <th>Duration</th>
                  <th>Days</th>
                  <th>Reason / Urgency</th>
                  <th>Status</th>
                  <th style={{ textAlign: 'right' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {requests.map((req) => {
                  const isOwnRequest = currentUserId === req.staff_member_id;
                  const daysCount = calculateDays(req.start_date, req.end_date);

                  return (
                    <tr key={req.id}>
                      <td>
                        <strong>{req.staff_name}</strong>
                        <br />
                        <span className="muted" style={{ fontSize: '0.75rem' }}>
                          Submitted {localDateTime(req.created_at)}
                        </span>
                      </td>
                      <td>
                        <span className="badge retired">
                          {leaveTypeLabels[req.type] ?? req.type}
                        </span>
                        {req.type === 'shift_swap' && (
                          <div className="muted" style={{ fontSize: '0.75rem', marginTop: '0.2rem' }}>
                            Shift swap
                          </div>
                        )}
                      </td>
                      <td>
                        <strong>{req.start_date}</strong> to <strong>{req.end_date}</strong>
                      </td>
                      <td>
                        <span>{daysCount} {daysCount === 1 ? 'day' : 'days'}</span>
                      </td>
                      <td style={{ maxWidth: '16rem' }}>
                        {req.is_urgent && (
                          <span
                            className="badge severity-critical"
                            style={{ marginRight: '0.35rem', verticalAlign: 'middle' }}
                          >
                            Urgent
                          </span>
                        )}
                        <span style={{ fontSize: '0.85rem' }}>
                          {req.reason ? req.reason : <span className="muted">No reason provided</span>}
                        </span>
                      </td>
                      <td>
                        <span className={leaveStatusTones[req.status]}>
                          {leaveStatusLabels[req.status] ?? req.status}
                        </span>
                      </td>
                      <td>
                        <div className="actions" style={{ justifyContent: 'flex-end' }}>
                          <button
                            type="button"
                            className="secondary small"
                            onClick={() => setDetailRequestId(req.id)}
                          >
                            Details
                          </button>

                          {req.status === 'pending' && !isOwnRequest && (
                            <>
                              <button
                                type="button"
                                className="small"
                                onClick={() =>
                                  setDecisionModal({ request: req, action: 'approve' })
                                }
                              >
                                Approve
                              </button>
                              <button
                                type="button"
                                className="secondary danger small"
                                onClick={() =>
                                  setDecisionModal({ request: req, action: 'reject' })
                                }
                              >
                                Reject
                              </button>
                            </>
                          )}

                          {req.status === 'pending' && isOwnRequest && (
                            <span className="muted" style={{ fontSize: '0.75rem', alignSelf: 'center' }}>
                              Own request
                            </span>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>

            {totalPages > 1 && (
              <div className="pager">
                <button
                  type="button"
                  className="secondary"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  Previous
                </button>
                <span className="muted">
                  Page {paged?.page ?? page} of {totalPages} · {paged?.total_items ?? 0} requests
                </span>
                <button
                  type="button"
                  className="secondary"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                >
                  Next
                </button>
              </div>
            )}
          </>
        )}
      </div>

      {/* Modal: Complete Leave Request Details */}
      {detailRequestId && (
        <LeaveRequestDetailModal
          requestId={detailRequestId}
          currentUserId={currentUserId}
          onClose={() => setDetailRequestId(null)}
          onDecide={(detail, action) => {
            setDetailRequestId(null);
            setDecisionModal({ request: detail, action });
          }}
        />
      )}

      {/* Modal: Confirm Leave Decision (Approve / Reject) */}
      {decisionModal && (
        <DecideLeaveModal
          request={decisionModal.request}
          action={decisionModal.action}
          onClose={() => setDecisionModal(null)}
          onSuccess={() => {
            setDecisionModal(null);
            invalidateQueries();
          }}
        />
      )}
    </>
  );
}

// -------------------------------------------------------------
// Helper: calculate number of days inclusive
// -------------------------------------------------------------
function calculateDays(startDate: string, endDate: string): number {
  try {
    const s = new Date(startDate).getTime();
    const e = new Date(endDate).getTime();
    if (isNaN(s) || isNaN(e)) return 1;
    const diffDays = Math.round((e - s) / (1000 * 60 * 60 * 24)) + 1;
    return diffDays > 0 ? diffDays : 1;
  } catch {
    return 1;
  }
}

// -------------------------------------------------------------
// Modal Dialog Shell
// -------------------------------------------------------------
function ModalDialog({
  title,
  onClose,
  maxWidth = '32rem',
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
// Leave Request Detail Modal
// -------------------------------------------------------------
function LeaveRequestDetailModal({
  requestId,
  currentUserId,
  onClose,
  onDecide,
}: {
  requestId: string;
  currentUserId?: string;
  onClose: () => void;
  onDecide: (detail: LeaveRequestDetailDto, action: 'approve' | 'reject') => void;
}) {
  const detailQuery = useQuery(getLeaveRequestOptions({ path: { id: requestId } }));
  const detail = detailQuery.data;

  const isOwnRequest = currentUserId && detail?.staff_member_id === currentUserId;
  const days = detail ? calculateDays(detail.start_date, detail.end_date) : 1;

  return (
    <ModalDialog
      title={detail ? `Leave request: ${detail.staff_name}` : 'Leave request details'}
      onClose={onClose}
      maxWidth="38rem"
    >
      {detailQuery.isLoading ? (
        <p className="empty">Loading request details…</p>
      ) : detailQuery.isError || !detail ? (
        <div className="empty">
          <p style={{ color: 'var(--danger)' }}>Could not load leave request details.</p>
          <button type="button" className="secondary" onClick={() => void detailQuery.refetch()}>
            Retry
          </button>
        </div>
      ) : (
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem', borderBottom: '1px solid var(--line)', paddingBottom: '0.75rem' }}>
            <div>
              <span className={leaveStatusTones[detail.status]} style={{ marginRight: '0.5rem' }}>
                {leaveStatusLabels[detail.status]}
              </span>
              <span className="badge retired">
                {leaveTypeLabels[detail.type]}
              </span>
              {detail.is_urgent && (
                <span className="badge severity-critical" style={{ marginLeft: '0.5rem' }}>
                  Urgent
                </span>
              )}
            </div>
            <span className="muted" style={{ fontSize: '0.85rem' }}>
              Submitted {localDateTime(detail.created_at)}
            </span>
          </div>

          <dl className="detail-grid">
            <div>
              <dt>Staff member</dt>
              <dd><strong>{detail.staff_name}</strong></dd>
            </div>
            <div>
              <dt>Staff UUID</dt>
              <dd style={{ fontSize: '0.75rem', fontFamily: 'monospace' }}>{detail.staff_member_id}</dd>
            </div>
            <div>
              <dt>Start date</dt>
              <dd><strong>{detail.start_date}</strong></dd>
            </div>
            <div>
              <dt>End date</dt>
              <dd><strong>{detail.end_date}</strong></dd>
            </div>
            <div>
              <dt>Total duration</dt>
              <dd>{days} {days === 1 ? 'day' : 'days'}</dd>
            </div>
            <div>
              <dt>Leave type</dt>
              <dd>{leaveTypeLabels[detail.type]}</dd>
            </div>
          </dl>

          {/* Reason */}
          <div style={{ marginTop: '1rem', background: 'var(--canvas)', padding: '0.75rem', borderRadius: '8px' }}>
            <span className="muted" style={{ display: 'block', fontSize: '0.75rem', fontWeight: 600, textTransform: 'uppercase' }}>
              Reason for request
            </span>
            <p style={{ margin: '0.35rem 0 0', fontSize: '0.9rem' }}>
              {detail.reason || <span className="muted">No detailed reason provided.</span>}
            </p>
          </div>

          {/* Review outcome if already decided */}
          {detail.status !== 'pending' && (
            <div style={{ marginTop: '1rem', borderTop: '1px solid var(--line)', paddingTop: '0.75rem' }}>
              <h3>Review outcome</h3>
              <p style={{ margin: '0.25rem 0', fontSize: '0.85rem' }}>
                Reviewed by: <code>{detail.reviewed_by_staff_id ?? 'System'}</code>{' '}
                {detail.reviewed_at && <>on {localDateTime(detail.reviewed_at)}</>}
              </p>
              {detail.review_notes && (
                <p className="muted" style={{ fontSize: '0.85rem', margin: '0.35rem 0' }}>
                  Review notes: &ldquo;{detail.review_notes}&rdquo;
                </p>
              )}
            </div>
          )}

          {/* Affected shifts and coverage impact */}
          <div style={{ marginTop: '1.25rem', borderTop: '1px solid var(--line)', paddingTop: '0.75rem' }}>
            <h3>Affected shifts & coverage impact ({detail.affected_shifts?.length ?? 0})</h3>
            {detail.affected_shifts && detail.affected_shifts.length > 0 ? (
              <>
                <p className="hint" style={{ marginTop: '0.2rem', marginBottom: '0.6rem' }}>
                  The staff member is currently allocated to the following shifts. Approving this request will release these allocations and trigger automatic roster proposals to fill open gaps.
                </p>
                <table>
                  <thead>
                    <tr>
                      <th>Shift date</th>
                      <th>Time</th>
                      <th>Ward</th>
                      <th>Coverage if approved</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.affected_shifts.map((item, idx) => (
                      <tr key={idx}>
                        <td>
                          <strong>{item.shift.date}</strong>
                        </td>
                        <td>
                          {item.shift.start_time} - {item.shift.end_time}
                        </td>
                        <td>{item.shift.ward_name}</td>
                        <td>
                          <span className={coverageStatusTones[item.coverage_if_approved.status]}>
                            {coverageStatusLabels[item.coverage_if_approved.status]}
                          </span>
                          <span className="muted" style={{ fontSize: '0.75rem', marginLeft: '0.4rem' }}>
                            ({item.coverage_if_approved.confirmed_count}/{item.coverage_if_approved.minimum_headcount} min)
                          </span>
                          {item.coverage_if_approved.shortfall_to_minimum > 0 && (
                            <span style={{ color: 'var(--danger)', fontSize: '0.75rem', display: 'block' }}>
                              -{item.coverage_if_approved.shortfall_to_minimum} below minimum
                            </span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </>
            ) : (
              <p className="muted" style={{ fontStyle: 'italic', margin: '0.35rem 0' }}>
                No active shift allocations conflict with this leave window.
              </p>
            )}
          </div>

          {/* Modal Actions */}
          <div className="actions" style={{ marginTop: '1.5rem', justifyContent: 'flex-end', borderTop: '1px solid var(--line)', paddingTop: '0.85rem' }}>
            <button type="button" className="secondary" onClick={onClose}>
              Close
            </button>

            {detail.status === 'pending' && !isOwnRequest && (
              <>
                <button
                  type="button"
                  className="secondary danger"
                  onClick={() => onDecide(detail, 'reject')}
                >
                  Reject request
                </button>
                <button
                  type="button"
                  onClick={() => onDecide(detail, 'approve')}
                >
                  Approve request
                </button>
              </>
            )}

            {detail.status === 'pending' && isOwnRequest && (
              <p className="muted" style={{ fontSize: '0.8rem', margin: 0, alignSelf: 'center' }}>
                You cannot review your own leave request.
              </p>
            )}
          </div>
        </div>
      )}
    </ModalDialog>
  );
}

// -------------------------------------------------------------
// Decide Leave Modal (Confirmation dialog for Approve / Reject)
// -------------------------------------------------------------
function DecideLeaveModal({
  request,
  action,
  onClose,
  onSuccess,
}: {
  request: LeaveRequestDto | LeaveRequestDetailDto;
  action: 'approve' | 'reject';
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [notes, setNotes] = useState('');

  const decideMutation = useMutation({
    ...decideLeaveRequestMutation(),
    onSuccess: (data) => {
      const isApproved = action === 'approve';
      toast.success(
        `Leave request for ${request.staff_name} has been ${isApproved ? 'approved' : 'rejected'}.`,
      );
      if (data.released_allocations && data.released_allocations.length > 0) {
        toast.info(
          `${data.released_allocations.length} shift allocation${
            data.released_allocations.length === 1 ? '' : 's'
          } released. Replacement roster proposals triggered.`,
        );
      }
      onSuccess();
    },
    onError: (err) => {
      const msg = (err as { detail?: string; title?: string })?.detail ??
        (err as { message?: string })?.message ??
        `Failed to ${action} leave request.`;
      toast.error(msg);
    },
  });

  function handleSubmit(event: FormEvent) {
    event.preventDefault();

    decideMutation.mutate({
      path: { id: request.id },
      body: {
        decision: action,
        notes: notes.trim() || null,
      },
    });
  }

  const isApprove = action === 'approve';

  return (
    <ModalDialog
      title={isApprove ? `Approve leave: ${request.staff_name}` : `Reject leave: ${request.staff_name}`}
      onClose={onClose}
      maxWidth="32rem"
    >
      <form onSubmit={handleSubmit}>
        <div style={{ marginBottom: '1rem' }}>
          <p>
            You are about to <strong>{isApprove ? 'approve' : 'reject'}</strong> the{' '}
            {leaveTypeLabels[request.type]} request for <strong>{request.staff_name}</strong> from{' '}
            <strong>{request.start_date}</strong> to <strong>{request.end_date}</strong>.
          </p>

          {isApprove ? (
            <div className="info-note" style={{ fontSize: '0.85rem' }}>
              <strong>Notice:</strong> Approving this leave request will automatically release any shift allocations during this time and generate replacement roster proposals.
            </div>
          ) : (
            <div className="stub-note" style={{ borderColor: 'var(--danger)', color: 'var(--danger)', background: '#fdf2f2', fontSize: '0.85rem' }}>
              <strong>Notice:</strong> Rejecting this request keeps all existing shift allocations intact. Please provide a clear explanation for the staff member below.
            </div>
          )}
        </div>

        <div style={{ marginTop: '0.85rem' }}>
          <label htmlFor="decision-notes">
            {isApprove ? 'Review notes (optional)' : 'Reason for rejection *'}
          </label>
          <textarea
            id="decision-notes"
            rows={3}
            required={!isApprove}
            placeholder={
              isApprove
                ? 'e.g. Approved. Ward coverage verified.'
                : 'e.g. Critical understaffing during these dates. Please coordinate with duty manager.'
            }
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
          />
        </div>

        <div className="actions" style={{ marginTop: '1.25rem', justifyContent: 'flex-end' }}>
          <button type="button" className="secondary" onClick={onClose} disabled={decideMutation.isPending}>
            Cancel
          </button>
          <button
            type="submit"
            className={isApprove ? undefined : 'danger'}
            disabled={decideMutation.isPending || (!isApprove && !notes.trim())}
          >
            {decideMutation.isPending
              ? isApprove
                ? 'Approving…'
                : 'Rejecting…'
              : isApprove
              ? 'Confirm approval'
              : 'Confirm rejection'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}
