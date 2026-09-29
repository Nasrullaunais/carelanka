import { useState } from 'react';
import type { FormEvent, ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  approveRosterProposalMutation,
  createRosterProposalMutation,
  getRosterProposalOptions,
  listRosterProposalsOptions,
  rejectRosterProposalMutation,
  reviseRosterProposalMutation,
} from '../../services/api/generated/@tanstack/react-query.gen';
import type {
  AgentOutcome,
  PlanStepDto,
  RejectionReason,
  RosterProposalDetail,
  RosterProposalErrorDto,
  RosterProposalStatus,
  RosterProposalSummary,
  RosterProposedChangeDto,
  RosterProposedChangeType,
  RosterValidationResult,
  ToolCallDto,
} from '../../services/api/generated';
import { useSession } from '../../services/auth/useSession';
import { localDateTime } from '../../types/datetime';
import { canManageStaff, canViewStaff } from '../../types/permissions';

const PAGE_SIZE = 15;

export const proposalStatusLabels: Record<RosterProposalStatus, string> = {
  pending: 'Pending (Planning)',
  pending_approval: 'Pending approval',
  approved: 'Approved',
  executed: 'Executed',
  rejected: 'Rejected',
  revision_requested: 'Revision requested',
  failed: 'Failed',
};

export const proposalStatusTones: Record<RosterProposalStatus, string> = {
  pending: 'badge severity-low',
  pending_approval: 'badge severity-high',
  approved: 'badge',
  executed: 'badge',
  rejected: 'badge severity-critical',
  revision_requested: 'badge retired',
  failed: 'badge severity-critical',
};

export const agentOutcomeLabels: Record<AgentOutcome, string> = {
  swap_proposed: 'Cascading swap',
  free_staff_proposed: 'Direct allocation',
  no_candidate_found: 'No candidate found',
  failed: 'Solver failed',
};

export const agentOutcomeTones: Record<AgentOutcome, string> = {
  swap_proposed: 'badge severity-low',
  free_staff_proposed: 'badge',
  no_candidate_found: 'badge severity-high',
  failed: 'badge severity-critical',
};

export const changeTypeLabels: Record<RosterProposedChangeType, string> = {
  end_allocation: 'End allocation',
  create_allocation: 'Create allocation',
};

export const rejectionReasonLabels: Record<RejectionReason, string> = {
  unsafe_suggestion: 'Unsafe suggestion (rule violation)',
  source_ward_cannot_spare: 'Source ward cannot spare staff',
  staff_unsuitable: 'Staff member unsuitable for shift',
  gap_filled_another_way: 'Gap filled another way',
  no_longer_needed: 'Shift gap no longer exists',
  other: 'Other reason',
};

export const rejectionReasons: RejectionReason[] = [
  'unsafe_suggestion',
  'source_ward_cannot_spare',
  'staff_unsuitable',
  'gap_filled_another_way',
  'no_longer_needed',
  'other',
];

export const proposalStatuses: RosterProposalStatus[] = [
  'pending',
  'pending_approval',
  'approved',
  'executed',
  'rejected',
  'revision_requested',
  'failed',
];

function getApiErrorMessage(err: unknown, fallbackMessage: string): string {
  if (!err) return fallbackMessage;
  const errorObj = err as {
    status?: number;
    statusText?: string;
    detail?: string;
    title?: string;
    message?: string;
    errors?: Record<string, string[]>;
  };

  if (errorObj.status === 409) {
    return 'This proposal may have changed or become invalid. Please refresh and review it again.';
  }
  if (errorObj.status === 403) {
    return 'You do not have permission to perform this action. Hospital administrator authorization is required.';
  }
  if (errorObj.status === 404) {
    return 'The requested roster proposal could not be found.';
  }
  if (errorObj.errors && typeof errorObj.errors === 'object') {
    const errorDetails = Object.values(errorObj.errors).flat().join(' ');
    if (errorDetails) return errorDetails;
  }
  if (errorObj.detail) return errorObj.detail;
  if (errorObj.message) return errorObj.message;
  if (errorObj.title) return errorObj.title;
  return fallbackMessage;
}

export function StaffRosterProposalsPage() {
  const session = useSession();
  const queryClient = useQueryClient();
  const role = session?.principal.role;

  const canView = canViewStaff(role);
  const canManage = canManageStaff(role);

  // Filters state
  const [selectedStatus, setSelectedStatus] = useState<RosterProposalStatus | 'all'>('all');
  const [wardIdFilter, setWardIdFilter] = useState('');
  const [shiftIdFilter, setShiftIdFilter] = useState('');
  const [page, setPage] = useState(1);

  // Modals state
  const [detailProposalId, setDetailProposalId] = useState<string | null>(null);
  const [approveProposal, setApproveProposal] = useState<RosterProposalSummary | RosterProposalDetail | null>(null);
  const [rejectProposal, setRejectProposal] = useState<RosterProposalSummary | RosterProposalDetail | null>(null);
  const [reviseProposal, setReviseProposal] = useState<RosterProposalSummary | RosterProposalDetail | null>(null);
  const [showCreateModal, setShowCreateModal] = useState(false);

  // Main proposals query
  const proposalsQuery = useQuery({
    ...listRosterProposalsOptions({
      query: {
        ...(selectedStatus !== 'all' ? { status: selectedStatus } : {}),
        ...(wardIdFilter.trim() ? { wardId: wardIdFilter.trim() } : {}),
        ...(shiftIdFilter.trim() ? { shiftId: shiftIdFilter.trim() } : {}),
        page,
        pageSize: PAGE_SIZE,
      },
    }),
    enabled: canView,
  });

  const invalidateProposalQueries = () => {
    void queryClient.invalidateQueries({
      predicate: (query) =>
        (query.queryKey[0] as { _id?: string } | undefined)?._id === 'listRosterProposals',
    });
    if (detailProposalId) {
      void queryClient.invalidateQueries({
        predicate: (query) =>
          (query.queryKey[0] as { _id?: string } | undefined)?._id === 'getRosterProposal',
      });
    }
  };

  if (!canView) {
    return (
      <>
        <h1>Roster Proposals</h1>
        <p className="empty">
          Your role cannot view staff roster proposals. Hospital administrators and duty managers have access.
        </p>
      </>
    );
  }

  function handleResetFilters() {
    setSelectedStatus('all');
    setWardIdFilter('');
    setShiftIdFilter('');
    setPage(1);
  }

  const paged = proposalsQuery.data;
  const proposals = paged?.items ?? [];
  const totalPages = paged?.total_pages ?? 1;

  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '1rem', marginBottom: '0.5rem' }}>
        <div>
          <h1>Roster Proposals</h1>
          <p className="muted">
            Proposals are generated to address staffing gaps and require human review before changes are applied.
          </p>
        </div>

        <div className="actions">
          <Link to="/staff" className="secondary button" style={{ textDecoration: 'none' }}>
            Staff directory
          </Link>
          <Link to="/staff/shifts" className="secondary button" style={{ textDecoration: 'none' }}>
            Shifts & roster
          </Link>
          <Link to="/staff/coverage" className="secondary button" style={{ textDecoration: 'none' }}>
            Ward coverage
          </Link>
          <Link to="/staff/leave-approval" className="secondary button" style={{ textDecoration: 'none' }}>
            Leave requests
          </Link>
          <Link to="/staff/reports" className="secondary button" style={{ textDecoration: 'none' }}>
            Staff reports
          </Link>
          <button
            type="button"
            onClick={() => setShowCreateModal(true)}
          >
            Create proposal
          </button>
          <button
            type="button"
            className="secondary"
            disabled={proposalsQuery.isFetching}
            onClick={() => void proposalsQuery.refetch()}
          >
            {proposalsQuery.isFetching ? 'Refreshing…' : 'Refresh'}
          </button>
        </div>
      </div>

      {/* Filter Card */}
      <div className="card">
        <h2>Filter proposals</h2>
        <div className="row">
          <div>
            <label htmlFor="proposal-status-filter">Review status</label>
            <select
              id="proposal-status-filter"
              value={selectedStatus}
              onChange={(e) => {
                setSelectedStatus(e.target.value as RosterProposalStatus | 'all');
                setPage(1);
              }}
            >
              <option value="all">All statuses</option>
              {proposalStatuses.map((s) => (
                <option key={s} value={s}>
                  {proposalStatusLabels[s]}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="proposal-ward-filter">Ward ID</label>
            <input
              id="proposal-ward-filter"
              type="text"
              placeholder="e.g. Ward UUID"
              value={wardIdFilter}
              onChange={(e) => {
                setWardIdFilter(e.target.value);
                setPage(1);
              }}
            />
          </div>

          <div>
            <label htmlFor="proposal-shift-filter">Shift ID</label>
            <input
              id="proposal-shift-filter"
              type="text"
              placeholder="e.g. Shift UUID"
              value={shiftIdFilter}
              onChange={(e) => {
                setShiftIdFilter(e.target.value);
                setPage(1);
              }}
            />
          </div>
        </div>

        {(selectedStatus !== 'all' || wardIdFilter || shiftIdFilter) && (
          <div className="actions" style={{ marginTop: '0.85rem' }}>
            <button type="button" className="secondary small" onClick={handleResetFilters}>
              Reset filters
            </button>
          </div>
        )}
      </div>

      {/* Proposals Table */}
      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.75rem' }}>
          <h2>
            Proposals {paged ? `(${paged.total_items})` : ''}
            {selectedStatus === 'pending_approval' && ' — Awaiting decision'}
          </h2>
        </div>

        {proposalsQuery.isLoading ? (
          <p className="empty">Loading roster proposals…</p>
        ) : proposalsQuery.isError ? (
          <div className="empty">
            <p style={{ color: 'var(--danger)' }}>
              {getApiErrorMessage(proposalsQuery.error, 'Failed to load roster proposals.')}
            </p>
            <button type="button" className="secondary" onClick={() => void proposalsQuery.refetch()}>
              Retry
            </button>
          </div>
        ) : proposals.length === 0 ? (
          <div className="empty">
            <p>No roster proposals found matching the selected filters.</p>
            {selectedStatus === 'pending_approval' && (
              <p className="muted" style={{ fontSize: '0.82rem' }}>
                All pending roster proposals have been reviewed.
              </p>
            )}
          </div>
        ) : (
          <>
            <table>
              <thead>
                <tr>
                  <th>Shift & Ward</th>
                  <th>Objective</th>
                  <th>Status</th>
                  <th>Outcome</th>
                  <th>Changes</th>
                  <th>Type</th>
                  <th>Created</th>
                  <th style={{ textAlign: 'right' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {proposals.map((proposal) => {
                  const statusKey = proposal.status ?? 'pending';
                  const outcomeKey = proposal.outcome;

                  return (
                    <tr key={proposal.id}>
                      <td>
                        <strong>{proposal.ward_name ?? 'Unassigned Ward'}</strong>
                        <br />
                        <span className="muted" style={{ fontSize: '0.75rem' }}>
                          Shift: {proposal.shift_date ?? 'N/A'}
                        </span>
                      </td>
                      <td style={{ maxWidth: '16rem' }}>
                        <span style={{ fontSize: '0.85rem' }}>
                          {proposal.objective || <span className="muted">Standard understaffing resolution</span>}
                        </span>
                      </td>
                      <td>
                        <span className={proposalStatusTones[statusKey]}>
                          {proposalStatusLabels[statusKey] ?? statusKey}
                        </span>
                      </td>
                      <td>
                        {outcomeKey ? (
                          <span className={agentOutcomeTones[outcomeKey]}>
                            {agentOutcomeLabels[outcomeKey] ?? outcomeKey}
                          </span>
                        ) : (
                          <span className="muted" style={{ fontSize: '0.8rem' }}>In progress</span>
                        )}
                      </td>
                      <td>
                        <strong>{proposal.change_count ?? 0}</strong>
                        <span className="muted" style={{ fontSize: '0.75rem', marginLeft: '0.25rem' }}>
                          {proposal.change_count === 1 ? 'change' : 'changes'}
                        </span>
                      </td>
                      <td>
                        {proposal.is_cascading_swap ? (
                          <span className="badge severity-low" title="Involves transferring staff from another ward">
                            Cascading swap
                          </span>
                        ) : (
                          <span className="badge retired">Direct</span>
                        )}
                      </td>
                      <td>
                        <span className="muted" style={{ fontSize: '0.75rem' }}>
                          {proposal.created_at ? localDateTime(proposal.created_at) : 'N/A'}
                        </span>
                      </td>
                      <td>
                        <div className="actions" style={{ justifyContent: 'flex-end' }}>
                          <button
                            type="button"
                            className="secondary small"
                            onClick={() => setDetailProposalId(proposal.id ?? null)}
                          >
                            Review
                          </button>

                          {proposal.status === 'pending_approval' && canManage && (
                            <>
                              <button
                                type="button"
                                className="small"
                                onClick={() => setApproveProposal(proposal)}
                              >
                                Approve
                              </button>
                              <button
                                type="button"
                                className="secondary danger small"
                                onClick={() => setRejectProposal(proposal)}
                              >
                                Reject
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
                  Page {paged?.page ?? page} of {totalPages} · {paged?.total_items ?? 0} proposals
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

      {/* Modal: Complete Proposal Details & Observability */}
      {detailProposalId && (
        <ProposalDetailModal
          proposalId={detailProposalId}
          canManage={canManage}
          onClose={() => setDetailProposalId(null)}
          onApprove={(detail) => {
            setDetailProposalId(null);
            setApproveProposal(detail);
          }}
          onReject={(detail) => {
            setDetailProposalId(null);
            setRejectProposal(detail);
          }}
          onRevise={(detail) => {
            setDetailProposalId(null);
            setReviseProposal(detail);
          }}
        />
      )}

      {/* Modal: Confirm Approval */}
      {approveProposal && (
        <ApproveProposalModal
          proposal={approveProposal}
          onClose={() => setApproveProposal(null)}
          onSuccess={() => {
            setApproveProposal(null);
            invalidateProposalQueries();
          }}
        />
      )}

      {/* Modal: Confirm Rejection */}
      {rejectProposal && (
        <RejectProposalModal
          proposal={rejectProposal}
          onClose={() => setRejectProposal(null)}
          onSuccess={() => {
            setRejectProposal(null);
            invalidateProposalQueries();
          }}
        />
      )}

      {/* Modal: Request Revision */}
      {reviseProposal && (
        <ReviseProposalModal
          proposal={reviseProposal}
          onClose={() => setReviseProposal(null)}
          onSuccess={() => {
            setReviseProposal(null);
            invalidateProposalQueries();
          }}
        />
      )}

      {/* Modal: Create Proposal */}
      {showCreateModal && (
        <CreateProposalModal
          onClose={() => setShowCreateModal(false)}
          onSuccess={() => {
            setShowCreateModal(false);
            invalidateProposalQueries();
          }}
        />
      )}
    </>
  );
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
// Human Approval Gate Workflow Banner
// -------------------------------------------------------------
function HumanApprovalGateBanner({ status }: { status: RosterProposalStatus }) {
  const isPending = status === 'pending';
  const isPendingApproval = status === 'pending_approval';
  const isApproved = status === 'approved' || status === 'executed';
  const isRejected = status === 'rejected';
  const isRevision = status === 'revision_requested';
  const isFailed = status === 'failed';

  return (
    <div
      style={{
        background: 'var(--canvas)',
        border: '1px solid var(--line)',
        borderRadius: '8px',
        padding: '0.85rem 1rem',
        marginBottom: '1rem',
      }}
    >
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
        <span style={{ fontSize: '0.75rem', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.05em' }}>
          Human-in-the-Loop Governance Workflow
        </span>
        <span className={proposalStatusTones[status]}>
          {proposalStatusLabels[status] ?? status}
        </span>
      </div>

      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: '0.5rem',
          fontSize: '0.82rem',
          flexWrap: 'wrap',
          color: 'var(--ink-soft)',
        }}
      >
        <span style={{ fontWeight: isPending ? 700 : 400, color: isPending ? 'var(--ink)' : undefined }}>
          1. Agent Plan
        </span>
        <span>→</span>
        <span style={{ fontWeight: isPendingApproval ? 700 : 400, color: isPendingApproval ? 'var(--ink)' : undefined }}>
          2. Deterministic Validation
        </span>
        <span>→</span>
        <span style={{ fontWeight: isPendingApproval ? 700 : 400, color: isPendingApproval ? 'var(--accent)' : undefined }}>
          3. Human Review Gate
        </span>
        <span>→</span>
        <span style={{
          fontWeight: isApproved || isRejected || isRevision || isFailed ? 700 : 400,
          color: isApproved ? 'var(--accent)' : isRejected || isFailed ? 'var(--danger)' : undefined,
        }}>
          4. {isApproved ? 'Approved & Executed' : isRejected ? 'Rejected' : isRevision ? 'Revision Requested' : isFailed ? 'Failed' : 'Decision'}
        </span>
      </div>

      <p className="hint" style={{ margin: '0.4rem 0 0', fontSize: '0.8rem' }}>
        {isPendingApproval
          ? 'Validation passed. Shift allocations are prepared but NOT applied until approved by a Hospital Administrator.'
          : isApproved
          ? 'Proposal has been approved. Shift allocations were applied atomically to the roster.'
          : isRejected
          ? 'Proposal was rejected with feedback. No changes were applied.'
          : isRevision
          ? 'Sent back to the solver agent with revision guidance and exclusion constraints.'
          : isPending
          ? 'Staff Allocation Agent is currently generating candidates and running safety checks.'
          : 'Planning failed or exceeded maximum revision limit.'}
      </p>
    </div>
  );
}

// -------------------------------------------------------------
// Proposal Detail Modal
// -------------------------------------------------------------
function ProposalDetailModal({
  proposalId,
  canManage,
  onClose,
  onApprove,
  onReject,
  onRevise,
}: {
  proposalId: string;
  canManage: boolean;
  onClose: () => void;
  onApprove: (detail: RosterProposalDetail) => void;
  onReject: (detail: RosterProposalDetail) => void;
  onRevise: (detail: RosterProposalDetail) => void;
}) {
  const [activeTab, setActiveTab] = useState<'changes' | 'plan' | 'validation' | 'tools' | 'review'>('changes');

  const detailQuery = useQuery(getRosterProposalOptions({ path: { id: proposalId } }));
  const detail = detailQuery.data;

  const status = detail?.status ?? 'pending';
  const isApprovable = status === 'pending_approval';

  return (
    <ModalDialog
      title={detail ? `Roster proposal: ${detail.ward_name ?? 'Shift'}` : 'Roster proposal details'}
      onClose={onClose}
      maxWidth="48rem"
    >
      {detailQuery.isLoading ? (
        <p className="empty">Loading proposal observability details…</p>
      ) : detailQuery.isError || !detail ? (
        <div className="empty">
          <p style={{ color: 'var(--danger)' }}>
            {getApiErrorMessage(detailQuery.error, 'Could not load roster proposal details.')}
          </p>
          <button type="button" className="secondary" onClick={() => void detailQuery.refetch()}>
            Retry
          </button>
        </div>
      ) : (
        <div>
          {/* Workflow Banner */}
          <HumanApprovalGateBanner status={status} />

          {/* Meta Grid */}
          <dl className="detail-grid">
            <div>
              <dt>Ward</dt>
              <dd><strong>{detail.ward_name ?? 'N/A'}</strong></dd>
            </div>
            <div>
              <dt>Shift date</dt>
              <dd><strong>{detail.shift_date ?? 'N/A'}</strong></dd>
            </div>
            <div>
              <dt>Shift ID</dt>
              <dd style={{ fontSize: '0.75rem', fontFamily: 'monospace' }}>{detail.shift_id ?? 'N/A'}</dd>
            </div>
            <div>
              <dt>Objective</dt>
              <dd>{detail.objective || <span className="muted">Standard understaffed shift fill</span>}</dd>
            </div>
            <div>
              <dt>Outcome</dt>
              <dd>
                {detail.outcome ? (
                  <span className={agentOutcomeTones[detail.outcome]}>
                    {agentOutcomeLabels[detail.outcome] ?? detail.outcome}
                  </span>
                ) : (
                  <span className="muted">Pending</span>
                )}
              </dd>
            </div>
            <div>
              <dt>Cascading swap</dt>
              <dd>
                {detail.is_cascading_swap ? (
                  <span className="badge severity-low">Yes (Donor ward needed)</span>
                ) : (
                  <span>No (Direct assignment)</span>
                )}
              </dd>
            </div>
            <div>
              <dt>Attempt count</dt>
              <dd><strong>{detail.attempt_count ?? 1}</strong></dd>
            </div>
            <div>
              <dt>Timestamps</dt>
              <dd style={{ fontSize: '0.75rem' }}>
                Created: {detail.created_at ? localDateTime(detail.created_at) : 'N/A'}
                {detail.completed_at && <> · Done: {localDateTime(detail.completed_at)}</>}
              </dd>
            </div>
          </dl>

          {/* Errors banner if present */}
          {detail.errors && detail.errors.length > 0 && (
            <div className="stub-note" style={{ borderColor: 'var(--danger)', color: 'var(--danger)', background: '#fdf2f2', margin: '1rem 0' }}>
              <strong>Execution / Validation Alerts:</strong>
              <ul style={{ margin: '0.4rem 0 0', paddingLeft: '1.2rem', fontSize: '0.85rem' }}>
                {detail.errors.map((err: RosterProposalErrorDto, idx: number) => (
                  <li key={idx}>
                    {err.step ? <strong>[{err.step}] </strong> : null}
                    {err.message ?? 'Unknown error'}
                    {err.occurred_at ? <span className="muted"> ({localDateTime(err.occurred_at)})</span> : null}
                  </li>
                ))}
              </ul>
            </div>
          )}

          {/* Subnavigation Tabs */}
          <div style={{ display: 'flex', gap: '0.5rem', borderBottom: '1px solid var(--line)', margin: '1.25rem 0 1rem' }}>
            <button
              type="button"
              className={activeTab === 'changes' ? undefined : 'secondary'}
              style={{ borderRadius: '6px 6px 0 0', borderBottom: activeTab === 'changes' ? '2px solid var(--accent)' : 'none' }}
              onClick={() => setActiveTab('changes')}
            >
              Proposed Changes ({detail.proposed_changes?.length ?? 0})
            </button>
            <button
              type="button"
              className={activeTab === 'plan' ? undefined : 'secondary'}
              style={{ borderRadius: '6px 6px 0 0', borderBottom: activeTab === 'plan' ? '2px solid var(--accent)' : 'none' }}
              onClick={() => setActiveTab('plan')}
            >
              Agent Plan ({detail.plan?.length ?? 0})
            </button>
            <button
              type="button"
              className={activeTab === 'validation' ? undefined : 'secondary'}
              style={{ borderRadius: '6px 6px 0 0', borderBottom: activeTab === 'validation' ? '2px solid var(--accent)' : 'none' }}
              onClick={() => setActiveTab('validation')}
            >
              Validation ({detail.validation?.length ?? 0})
            </button>
            <button
              type="button"
              className={activeTab === 'tools' ? undefined : 'secondary'}
              style={{ borderRadius: '6px 6px 0 0', borderBottom: activeTab === 'tools' ? '2px solid var(--accent)' : 'none' }}
              onClick={() => setActiveTab('tools')}
            >
              Tool Calls ({detail.tool_calls?.length ?? 0})
            </button>
            {(detail.reviewed_at || detail.review_notes || detail.rejection_reason) && (
              <button
                type="button"
                className={activeTab === 'review' ? undefined : 'secondary'}
                style={{ borderRadius: '6px 6px 0 0', borderBottom: activeTab === 'review' ? '2px solid var(--accent)' : 'none' }}
                onClick={() => setActiveTab('review')}
              >
                Review Log
              </button>
            )}
          </div>

          {/* Tab 1: Proposed Changes */}
          {activeTab === 'changes' && (
            <div>
              {detail.proposed_changes && detail.proposed_changes.length > 0 ? (
                <table>
                  <thead>
                    <tr>
                      <th>Seq</th>
                      <th>Change Action</th>
                      <th>Staff Member</th>
                      <th>Source → Target</th>
                      <th>Validation</th>
                      <th>Rationale</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.proposed_changes.map((change: RosterProposedChangeDto, idx: number) => {
                      const changeType = change.change_type ?? 'create_allocation';
                      const valStatus = change.validation_status ?? 'passed';

                      return (
                        <tr key={change.id ?? idx}>
                          <td>{change.sequence ?? idx + 1}</td>
                          <td>
                            <span className={changeType === 'create_allocation' ? 'badge' : 'badge severity-low'}>
                              {changeTypeLabels[changeType] ?? changeType}
                            </span>
                            {change.applied_at && (
                              <span className="badge" style={{ display: 'block', marginTop: '0.2rem', fontSize: '0.7rem' }}>
                                Applied
                              </span>
                            )}
                          </td>
                          <td>
                            <strong>{change.proposed_staff_name ?? change.staff_name ?? 'N/A'}</strong>
                            <br />
                            <span className="muted" style={{ fontSize: '0.7rem', fontFamily: 'monospace' }}>
                              {change.proposed_staff_member_id ?? change.staff_member_id ?? change.target_allocation_id}
                            </span>
                          </td>
                          <td>
                            {change.from_ward_name ? (
                              <span>
                                {change.from_ward_name} → <strong>{change.to_ward_name ?? detail.ward_name}</strong>
                              </span>
                            ) : (
                              <span>→ <strong>{change.to_ward_name ?? detail.ward_name}</strong></span>
                            )}
                          </td>
                          <td>
                            <span className={valStatus === 'passed' ? 'badge' : valStatus === 'failed' ? 'badge severity-critical' : 'badge severity-low'}>
                              {valStatus}
                            </span>
                            {change.validation_message && (
                              <span className="muted" style={{ display: 'block', fontSize: '0.75rem', marginTop: '0.2rem' }}>
                                {change.validation_message}
                              </span>
                            )}
                          </td>
                          <td style={{ maxWidth: '14rem', fontSize: '0.8rem' }}>
                            {change.rationale || <span className="muted">None</span>}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              ) : (
                <p className="empty">No domain changes proposed in this plan.</p>
              )}
            </div>
          )}

          {/* Tab 2: Agent Plan */}
          {activeTab === 'plan' && (
            <div>
              {detail.plan && detail.plan.length > 0 ? (
                <table>
                  <thead>
                    <tr>
                      <th>Seq</th>
                      <th>Agent Role</th>
                      <th>Description</th>
                      <th>Status</th>
                      <th>Timestamps</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.plan.map((step: PlanStepDto, idx: number) => (
                      <tr key={idx}>
                        <td>{step.sequence ?? step.step ?? idx + 1}</td>
                        <td>
                          <span className="badge retired">
                            {step.agent_role ?? 'SolverAgent'}
                          </span>
                        </td>
                        <td>{step.description ?? 'Execution step'}</td>
                        <td>
                          <span className={step.status === 'completed' ? 'badge' : step.status === 'failed' ? 'badge severity-critical' : 'badge severity-low'}>
                            {step.status ?? 'pending'}
                          </span>
                        </td>
                        <td style={{ fontSize: '0.75rem' }}>
                          {step.started_at ? localDateTime(step.started_at) : 'N/A'}
                          {step.completed_at && <> → {localDateTime(step.completed_at)}</>}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : (
                <p className="empty">No plan steps recorded.</p>
              )}
            </div>
          )}

          {/* Tab 3: Deterministic Validation */}
          {activeTab === 'validation' && (
            <div>
              {detail.validation && detail.validation.length > 0 ? (
                <table>
                  <thead>
                    <tr>
                      <th>Deterministic Check</th>
                      <th>Result</th>
                      <th>Details</th>
                      <th>Checked At</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.validation.map((v: RosterValidationResult, idx: number) => (
                      <tr key={idx}>
                        <td><strong>{v.check}</strong></td>
                        <td>
                          <span className={v.passed ? 'badge' : 'badge severity-critical'}>
                            {v.passed ? 'Passed' : 'Failed'}
                          </span>
                        </td>
                        <td style={{ fontSize: '0.85rem' }}>{v.detail}</td>
                        <td style={{ fontSize: '0.75rem' }}>{localDateTime(v.checked_at)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : (
                <p className="empty">No deterministic validation results recorded.</p>
              )}
            </div>
          )}

          {/* Tab 4: Tool Calls */}
          {activeTab === 'tools' && (
            <div>
              {detail.tool_calls && detail.tool_calls.length > 0 ? (
                <table>
                  <thead>
                    <tr>
                      <th>Tool Name</th>
                      <th>Duration</th>
                      <th>Result</th>
                      <th>Summary / Arguments</th>
                      <th>Invoked At</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.tool_calls.map((tc: ToolCallDto, idx: number) => (
                      <tr key={idx}>
                        <td>
                          <code>{tc.tool_name ?? tc.tool ?? 'tool'}</code>
                        </td>
                        <td>{tc.duration_ms !== undefined ? `${tc.duration_ms}ms` : 'N/A'}</td>
                        <td>
                          <span className={tc.succeeded ? 'badge' : 'badge severity-critical'}>
                            {tc.succeeded ? 'Success' : 'Error'}
                          </span>
                        </td>
                        <td style={{ maxWidth: '18rem', fontSize: '0.8rem' }}>
                          {tc.summary || (tc.arguments ? JSON.stringify(tc.arguments) : 'No arguments')}
                          {tc.error && (
                            <span style={{ color: 'var(--danger)', display: 'block', marginTop: '0.2rem' }}>
                              {tc.error}
                            </span>
                          )}
                        </td>
                        <td style={{ fontSize: '0.75rem' }}>
                          {tc.called_at ? localDateTime(tc.called_at) : 'N/A'}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : (
                <p className="empty">No tool invocations recorded.</p>
              )}
            </div>
          )}

          {/* Tab 5: Review Log */}
          {activeTab === 'review' && (
            <div style={{ background: 'var(--canvas)', padding: '1rem', borderRadius: '8px' }}>
              <h3>Review Outcome & Decision Log</h3>
              <dl className="detail-grid" style={{ marginTop: '0.5rem' }}>
                <div>
                  <dt>Reviewed By</dt>
                  <dd><code>{detail.reviewed_by_staff_id ?? detail.reviewed_by_staff_member_id ?? 'System'}</code></dd>
                </div>
                <div>
                  <dt>Reviewed At</dt>
                  <dd>{detail.reviewed_at ? localDateTime(detail.reviewed_at) : 'N/A'}</dd>
                </div>
                {detail.rejection_reason && (
                  <div>
                    <dt>Rejection Reason</dt>
                    <dd>
                      <span className="badge severity-critical">
                        {rejectionReasonLabels[detail.rejection_reason] ?? detail.rejection_reason}
                      </span>
                    </dd>
                  </div>
                )}
                {detail.final_outcome && (
                  <div>
                    <dt>Final Outcome</dt>
                    <dd>{detail.final_outcome}</dd>
                  </div>
                )}
              </dl>
              {detail.review_notes && (
                <div style={{ marginTop: '0.75rem', borderTop: '1px solid var(--line)', paddingTop: '0.5rem' }}>
                  <span className="muted" style={{ fontSize: '0.75rem', fontWeight: 600, textTransform: 'uppercase' }}>
                    Review Notes:
                  </span>
                  <p style={{ margin: '0.25rem 0 0', fontSize: '0.9rem' }}>&ldquo;{detail.review_notes}&rdquo;</p>
                </div>
              )}
            </div>
          )}

          {/* Modal Actions */}
          <div className="actions" style={{ marginTop: '1.5rem', justifyContent: 'flex-end', borderTop: '1px solid var(--line)', paddingTop: '0.85rem' }}>
            <button type="button" className="secondary" onClick={onClose}>
              Close
            </button>

            {isApprovable && canManage && (
              <>
                <button
                  type="button"
                  className="secondary danger"
                  onClick={() => onReject(detail)}
                >
                  Reject proposal
                </button>
                <button
                  type="button"
                  className="secondary"
                  onClick={() => onRevise(detail)}
                >
                  Request revision
                </button>
                <button
                  type="button"
                  onClick={() => onApprove(detail)}
                >
                  Approve proposal
                </button>
              </>
            )}

            {isApprovable && !canManage && (
              <p className="muted" style={{ fontSize: '0.8rem', margin: 0, alignSelf: 'center' }}>
                Hospital Administrator permission required to approve or reject.
              </p>
            )}
          </div>
        </div>
      )}
    </ModalDialog>
  );
}

// -------------------------------------------------------------
// Approve Proposal Modal
// -------------------------------------------------------------
function ApproveProposalModal({
  proposal,
  onClose,
  onSuccess,
}: {
  proposal: RosterProposalSummary | RosterProposalDetail;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [notes, setNotes] = useState('');

  const approveMutation = useMutation({
    ...approveRosterProposalMutation(),
    onSuccess: () => {
      toast.success('Roster proposal approved. All shift allocations have been atomically applied.');
      onSuccess();
    },
    onError: (err) => {
      toast.error(getApiErrorMessage(err, 'Failed to approve roster proposal.'));
    },
  });

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!proposal.id) return;

    approveMutation.mutate({
      path: { id: proposal.id },
      body: {
        notes: notes.trim() || null,
      },
    });
  }

  return (
    <ModalDialog
      title="Approve roster proposal"
      onClose={onClose}
      maxWidth="32rem"
    >
      <form onSubmit={handleSubmit}>
        <div style={{ marginBottom: '1rem' }}>
          <p>
            You are approving the roster proposal for <strong>{proposal.ward_name ?? 'the ward'}</strong> on shift date <strong>{proposal.shift_date ?? 'N/A'}</strong>.
          </p>

          <div className="info-note" style={{ fontSize: '0.85rem' }}>
            <strong>Atomicity Notice:</strong> Approving this proposal will atomically execute all {proposal.change_count ?? 0} proposed allocations/reassignments in the database.
          </div>
        </div>

        <div style={{ marginTop: '0.85rem' }}>
          <label htmlFor="approve-notes">Approval notes (optional)</label>
          <textarea
            id="approve-notes"
            rows={3}
            placeholder="e.g. Approved. Reviewed ward requirements and verified coverage."
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
          />
        </div>

        <div className="actions" style={{ marginTop: '1.25rem', justifyContent: 'flex-end' }}>
          <button type="button" className="secondary" onClick={onClose} disabled={approveMutation.isPending}>
            Cancel
          </button>
          <button
            type="submit"
            disabled={approveMutation.isPending}
          >
            {approveMutation.isPending ? 'Applying allocations…' : 'Confirm approval'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}

// -------------------------------------------------------------
// Reject Proposal Modal
// -------------------------------------------------------------
function RejectProposalModal({
  proposal,
  onClose,
  onSuccess,
}: {
  proposal: RosterProposalSummary | RosterProposalDetail;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [reason, setReason] = useState<RejectionReason>('unsafe_suggestion');
  const [notes, setNotes] = useState('');

  const rejectMutation = useMutation({
    ...rejectRosterProposalMutation(),
    onSuccess: () => {
      toast.success('Roster proposal rejected.');
      onSuccess();
    },
    onError: (err) => {
      toast.error(getApiErrorMessage(err, 'Failed to reject roster proposal.'));
    },
  });

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!proposal.id) return;

    rejectMutation.mutate({
      path: { id: proposal.id },
      body: {
        reason,
        notes: notes.trim() || null,
      },
    });
  }

  return (
    <ModalDialog
      title="Reject roster proposal"
      onClose={onClose}
      maxWidth="32rem"
    >
      <form onSubmit={handleSubmit}>
        <div style={{ marginBottom: '1rem' }}>
          <p>
            You are rejecting the roster proposal for <strong>{proposal.ward_name ?? 'the ward'}</strong> on <strong>{proposal.shift_date ?? 'N/A'}</strong>.
          </p>

          <div className="stub-note" style={{ borderColor: 'var(--danger)', color: 'var(--danger)', background: '#fdf2f2', fontSize: '0.85rem' }}>
            <strong>Auditing Notice:</strong> Rejection reasons feed the agent performance metrics to identify gaps in deterministic checks.
          </div>
        </div>

        <div style={{ marginTop: '0.85rem' }}>
          <label htmlFor="reject-reason">Rejection reason *</label>
          <select
            id="reject-reason"
            value={reason}
            onChange={(e) => setReason(e.target.value as RejectionReason)}
            required
          >
            {rejectionReasons.map((r) => (
              <option key={r} value={r}>
                {rejectionReasonLabels[r]}
              </option>
            ))}
          </select>
        </div>

        <div style={{ marginTop: '0.85rem' }}>
          <label htmlFor="reject-notes">Reviewer notes (optional)</label>
          <textarea
            id="reject-notes"
            rows={3}
            placeholder="e.g. Donor ward would fall to bare minimum headcount. Recommend external agency staff instead."
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
          />
        </div>

        <div className="actions" style={{ marginTop: '1.25rem', justifyContent: 'flex-end' }}>
          <button type="button" className="secondary" onClick={onClose} disabled={rejectMutation.isPending}>
            Cancel
          </button>
          <button
            type="submit"
            className="danger"
            disabled={rejectMutation.isPending}
          >
            {rejectMutation.isPending ? 'Rejecting…' : 'Confirm rejection'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}

// -------------------------------------------------------------
// Request Revision Modal
// -------------------------------------------------------------
function ReviseProposalModal({
  proposal,
  onClose,
  onSuccess,
}: {
  proposal: RosterProposalSummary | RosterProposalDetail;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [guidance, setGuidance] = useState('');
  const [notes, setNotes] = useState('');
  const [excludeStaffInput, setExcludeStaffInput] = useState('');
  const [excludeWardInput, setExcludeWardInput] = useState('');

  const reviseMutation = useMutation({
    ...reviseRosterProposalMutation(),
    onSuccess: () => {
      toast.success('Revision requested. The Staff Allocation Agent will replan with your guidance.');
      onSuccess();
    },
    onError: (err) => {
      toast.error(getApiErrorMessage(err, 'Failed to request revision.'));
    },
  });

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!proposal.id || !guidance.trim()) return;

    const excludeStaffIds = excludeStaffInput
      .split(',')
      .map((s) => s.trim())
      .filter((s) => s.length > 0);

    const excludeWardIds = excludeWardInput
      .split(',')
      .map((w) => w.trim())
      .filter((w) => w.length > 0);

    reviseMutation.mutate({
      path: { id: proposal.id },
      body: {
        guidance: guidance.trim(),
        notes: notes.trim() || null,
        exclude_staff_ids: excludeStaffIds.length > 0 ? excludeStaffIds : null,
        exclude_ward_ids: excludeWardIds.length > 0 ? excludeWardIds : null,
      },
    });
  }

  return (
    <ModalDialog
      title="Request proposal revision"
      onClose={onClose}
      maxWidth="34rem"
    >
      <form onSubmit={handleSubmit}>
        <div style={{ marginBottom: '1rem' }}>
          <p>
            Send this proposal back to the Staff Allocation Agent for automated replanning with your constraints.
          </p>
          <div className="info-note" style={{ fontSize: '0.85rem' }}>
            <strong>Replanning:</strong> The proposal returns to planning status with an incremented attempt count.
          </div>
        </div>

        <div style={{ marginTop: '0.85rem' }}>
          <label htmlFor="revise-guidance">Revision guidance * (max 500 chars)</label>
          <textarea
            id="revise-guidance"
            rows={3}
            maxLength={500}
            required
            placeholder="e.g. Do not transfer staff out of ICU. Prefer available general ward nurses."
            value={guidance}
            onChange={(e) => setGuidance(e.target.value)}
          />
          <span className="muted" style={{ fontSize: '0.75rem', display: 'block', textAlign: 'right' }}>
            {guidance.length} / 500 characters
          </span>
        </div>

        <div style={{ marginTop: '0.85rem' }}>
          <label htmlFor="revise-staff-exclusion">Exclude staff IDs (optional, comma-separated UUIDs)</label>
          <input
            id="revise-staff-exclusion"
            type="text"
            placeholder="e.g. 3fa85f64-5717-4562-b3fc-2c963f66afa6"
            value={excludeStaffInput}
            onChange={(e) => setExcludeStaffInput(e.target.value)}
          />
        </div>

        <div style={{ marginTop: '0.85rem' }}>
          <label htmlFor="revise-ward-exclusion">Exclude ward IDs (optional, comma-separated UUIDs)</label>
          <input
            id="revise-ward-exclusion"
            type="text"
            placeholder="e.g. 7ba45f64-1234-4562-b3fc-2c963f66afa6"
            value={excludeWardInput}
            onChange={(e) => setExcludeWardInput(e.target.value)}
          />
        </div>

        <div style={{ marginTop: '0.85rem' }}>
          <label htmlFor="revise-notes">Additional review notes (optional)</label>
          <textarea
            id="revise-notes"
            rows={2}
            placeholder="Internal reviewer notes"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
          />
        </div>

        <div className="actions" style={{ marginTop: '1.25rem', justifyContent: 'flex-end' }}>
          <button type="button" className="secondary" onClick={onClose} disabled={reviseMutation.isPending}>
            Cancel
          </button>
          <button
            type="submit"
            disabled={reviseMutation.isPending || !guidance.trim()}
          >
            {reviseMutation.isPending ? 'Sending to agent…' : 'Submit revision request'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}

// -------------------------------------------------------------
// Create Proposal Modal
// -------------------------------------------------------------
function CreateProposalModal({
  onClose,
  onSuccess,
}: {
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [shiftId, setShiftId] = useState('');
  const [objective, setObjective] = useState('');
  const [allowCascadingSwap, setAllowCascadingSwap] = useState(true);

  const createMutation = useMutation({
    ...createRosterProposalMutation(),
    onSuccess: (data) => {
      toast.success(
        `Staff Allocation Agent dispatched for shift ${shiftId.slice(0, 8)}… Proposal status: ${data.status ?? 'pending'}.`,
      );
      onSuccess();
    },
    onError: (err) => {
      toast.error(getApiErrorMessage(err, 'Failed to trigger proposal generation.'));
    },
  });

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!shiftId.trim()) return;

    createMutation.mutate({
      body: {
        shift_id: shiftId.trim(),
        objective: objective.trim() || null,
        allow_cascading_swap: allowCascadingSwap,
      },
    });
  }

  return (
    <ModalDialog
      title="Request roster proposal"
      onClose={onClose}
      maxWidth="32rem"
    >
      <form onSubmit={handleSubmit}>
        <div style={{ marginBottom: '1rem' }}>
          <p>
            Instruct the <strong>Staff Allocation Agent</strong> to analyze constraints, evaluate qualified personnel, and solve an understaffed shift gap.
          </p>
        </div>

        <div style={{ marginTop: '0.85rem' }}>
          <label htmlFor="create-shift-id">Shift ID * (UUID)</label>
          <input
            id="create-shift-id"
            type="text"
            required
            placeholder="e.g. 550e8400-e29b-41d4-a716-446655440000"
            value={shiftId}
            onChange={(e) => setShiftId(e.target.value)}
          />
          <span className="hint" style={{ fontSize: '0.75rem', marginTop: '0.2rem', display: 'block' }}>
            The unique identifier of the understaffed or critical shift requiring staff allocation.
          </span>
        </div>

        <div style={{ marginTop: '0.85rem' }}>
          <label htmlFor="create-objective">Solving objective / notes (optional)</label>
          <input
            id="create-objective"
            type="text"
            placeholder="e.g. Fill ICU night shift nurse deficit after sick leave"
            value={objective}
            onChange={(e) => setObjective(e.target.value)}
          />
        </div>

        <div style={{ marginTop: '1rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <input
            id="create-cascading"
            type="checkbox"
            checked={allowCascadingSwap}
            onChange={(e) => setAllowCascadingSwap(e.target.checked)}
          />
          <label htmlFor="create-cascading" style={{ margin: 0, fontWeight: 500, cursor: 'pointer' }}>
            Allow cascading swap from donor wards if no unallocated staff are free
          </label>
        </div>

        <div className="actions" style={{ marginTop: '1.25rem', justifyContent: 'flex-end' }}>
          <button type="button" className="secondary" onClick={onClose} disabled={createMutation.isPending}>
            Cancel
          </button>
          <button
            type="submit"
            disabled={createMutation.isPending || !shiftId.trim()}
          >
            {createMutation.isPending ? 'Invoking agent…' : 'Generate proposal'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}
