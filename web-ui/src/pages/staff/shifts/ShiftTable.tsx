import type { ShiftSummaryDto } from '../../../services/api/generated';
import { coverageStatusLabels, coverageStatusTones, staffRoleLabels } from '../../../types/staff';

/**
 * Shifts data table with pagination controls.
 */
export function ShiftTable({
  shifts,
  totalItems,
  totalPages,
  page,
  isFetching,
  isPending,
  isError,
  error,
  canManage,
  onViewShift,
  onEditShift,
  onCancelShift,
  onCreateShift,
  onPageChange,
}: {
  shifts: ShiftSummaryDto[];
  totalItems: number | undefined;
  totalPages: number;
  page: number;
  isFetching: boolean;
  isPending: boolean;
  isError: boolean;
  error: Error | null;
  canManage: boolean;
  onViewShift: (id: string) => void;
  onEditShift: (shift: ShiftSummaryDto) => void;
  onCancelShift: (shift: ShiftSummaryDto) => void;
  onCreateShift: () => void;
  onPageChange: (page: number) => void;
}) {
  return (
    <div className="card">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem', flexWrap: 'wrap', gap: '0.5rem' }}>
        <h2>
          Scheduled shifts {totalItems !== undefined ? `(${totalItems})` : ''}
        </h2>
        {isFetching && <span className="muted" style={{ fontSize: '0.85rem' }}>Refreshing shifts…</span>}
      </div>

      {isPending ? (
        <p className="muted">Loading scheduled shifts…</p>
      ) : isError ? (
        <p className="empty">
          Failed to load shifts:{' '}
          {error instanceof Error ? error.message : 'Unknown error'}
        </p>
      ) : shifts.length === 0 ? (
        <div className="empty" style={{ padding: '2rem 1rem', textAlign: 'center' }}>
          <p>No scheduled shifts found matching the selected filters.</p>
          {canManage && (
            <div style={{ marginTop: '0.75rem' }}>
              <button type="button" onClick={onCreateShift}>
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
                {shifts.map((shift) => {
                  const coverage = shift.coverage;
                  const statusTone = coverage ? coverageStatusTones[coverage.status] : 'badge';
                  const statusLabel = coverage ? coverageStatusLabels[coverage.status] : 'Unknown';

                  return (
                    <tr key={shift.id}>
                      <td><strong>{shift.date}</strong></td>
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
                            onClick={() => onViewShift(shift.id)}
                          >
                            View
                          </button>
                          {canManage && (
                            <>
                              <button
                                type="button"
                                className="secondary small"
                                onClick={() => onEditShift(shift)}
                              >
                                Edit
                              </button>
                              <button
                                type="button"
                                className="secondary small"
                                style={{ color: 'var(--danger, #b91c1c)' }}
                                onClick={() => onCancelShift(shift)}
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

          {totalPages > 1 && (
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '1rem', flexWrap: 'wrap', gap: '0.5rem' }}>
              <span className="muted" style={{ fontSize: '0.85rem' }}>
                Page {page} of {totalPages} ({totalItems ?? 0} shifts)
              </span>
              <div style={{ display: 'flex', gap: '0.5rem' }}>
                <button
                  type="button"
                  className="secondary small"
                  disabled={page <= 1}
                  onClick={() => onPageChange(Math.max(1, page - 1))}
                >
                  Previous
                </button>
                <button
                  type="button"
                  className="secondary small"
                  disabled={page >= totalPages}
                  onClick={() => onPageChange(Math.min(totalPages, page + 1))}
                >
                  Next
                </button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
