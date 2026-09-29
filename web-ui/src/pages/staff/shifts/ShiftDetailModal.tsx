import { useQuery, useQueryClient } from '@tanstack/react-query';
import { getShiftOptions } from '../../../services/api/generated/@tanstack/react-query.gen';
import type { ShiftDetailDto } from '../../../services/api/generated';
import { coverageStatusLabels, coverageStatusTones } from '../../../types/staff';
import { ModalDialog } from './ModalDialog';
import { AllocationPanel } from './AllocationPanel';

export function ShiftDetailModal({
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

  const detailQuery = useQuery(getShiftOptions({ path: { id: shiftId } }));
  const shift = detailQuery.data;

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

  const activeAllocations = (shift.allocations ?? []).filter(
    (a) => !a.ended_at && a.status !== 'released' && a.status !== 'cancelled'
  );

  return (
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

        {/* Shift details */}
        <dl className="grid-list" style={{ margin: 0 }}>
          <div><dt>Ward</dt><dd><strong>{shift.ward_name}</strong></dd></div>
          <div><dt>Date</dt><dd>{shift.date}</dd></div>
          <div><dt>Required role</dt><dd>{shift.required_role}</dd></div>
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
          <div><dt>Headcount needed</dt><dd><strong>{shift.headcount_needed}</strong> staff</dd></div>
          <div><dt>Minimum headcount</dt><dd><strong>{shift.minimum_headcount}</strong> staff</dd></div>
          <div>
            <dt>Currently allocated</dt>
            <dd><strong>{coverage?.confirmed_count ?? activeAllocations.length}</strong> confirmed</dd>
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

        {/* Allocation panel (Group 2) */}
        <AllocationPanel shift={shift} canManage={canManage} onDataChanged={invalidateData} />

        {/* Modal actions footer */}
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
  );
}
