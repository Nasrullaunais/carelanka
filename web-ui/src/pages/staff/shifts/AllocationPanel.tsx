import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { listAllocationsOptions } from '../../../services/api/generated/@tanstack/react-query.gen';
import type { AllocationDto, ShiftDetailDto } from '../../../services/api/generated';
import { localDateTime } from '../../../types/datetime';
import { allocationEndReasonLabels, allocationSourceLabels, allocationStatusLabels, allocationStatusTones } from './constants';
import { AssignStaffModal } from './AssignStaffModal';
import { EndAllocationModal } from './EndAllocationModal';

/**
 * Allocated staff panel rendered inside the ShiftDetailModal.
 * Handles listAllocations query, toggling ended history,
 * and opening Assign / End Allocation sub-modals.
 */
export function AllocationPanel({
  shift,
  canManage,
  onDataChanged,
}: {
  shift: ShiftDetailDto;
  canManage: boolean;
  onDataChanged: () => void;
}) {
  const [showAssignModal, setShowAssignModal] = useState(false);
  const [endingAllocation, setEndingAllocation] = useState<AllocationDto | null>(null);
  const [showEndedAllocations, setShowEndedAllocations] = useState(true);

  const allocationsQuery = useQuery({
    ...listAllocationsOptions({ query: { shiftId: shift.id } }),
    enabled: Boolean(shift.id),
  });

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
                            <span className="muted" style={{ fontSize: '0.75rem' }}>Ended</span>
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

      {/* Open proposal link */}
      {shift.open_proposal_id && (
        <div
          className="card"
          style={{
            padding: '0.75rem 1rem',
            background: 'rgba(217, 119, 6, 0.08)',
            border: '1px solid rgba(217, 119, 6, 0.3)',
          }}
        >
          <strong style={{ color: 'var(--warning, #b45309)' }}>Active AI Roster Proposal</strong>
          <p className="muted" style={{ margin: '0.25rem 0 0.5rem', fontSize: '0.85rem' }}>
            The AI Roster Agent has raised a swap/fill proposal to resolve staffing for this shift.
          </p>
          <Link
            to="/staff/roster-proposals"
            className="button secondary small"
            style={{ textDecoration: 'none' }}
          >
            Review roster proposal →
          </Link>
        </div>
      )}

      {showAssignModal && (
        <AssignStaffModal
          shift={shift}
          onClose={() => setShowAssignModal(false)}
          onSuccess={() => {
            setShowAssignModal(false);
            onDataChanged();
          }}
        />
      )}

      {endingAllocation && (
        <EndAllocationModal
          allocation={endingAllocation}
          shift={shift}
          onClose={() => setEndingAllocation(null)}
          onSuccess={() => {
            setEndingAllocation(null);
            onDataChanged();
          }}
        />
      )}
    </>
  );
}
