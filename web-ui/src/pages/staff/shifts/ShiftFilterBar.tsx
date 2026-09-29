import type { CoverageStatus, StaffRole } from '../../../services/api/generated';
import { coverageStatusLabels, staffRoleLabels, staffRoles } from '../../../types/staff';

/**
 * Shift filter bar rendered above the shifts table.
 */
export function ShiftFilterBar({
  wards,
  selectedWardId,
  onWardChange,
  fromDate,
  onFromDateChange,
  toDate,
  onToDateChange,
  selectedRole,
  onRoleChange,
  selectedCoverageStatus,
  onCoverageStatusChange,
  onReset,
}: {
  wards: Array<{ id: string; name: string }>;
  selectedWardId: string;
  onWardChange: (v: string) => void;
  fromDate: string;
  onFromDateChange: (v: string) => void;
  toDate: string;
  onToDateChange: (v: string) => void;
  selectedRole: StaffRole | '';
  onRoleChange: (v: StaffRole | '') => void;
  selectedCoverageStatus: CoverageStatus | '';
  onCoverageStatusChange: (v: CoverageStatus | '') => void;
  onReset: () => void;
}) {
  const hasActiveFilters =
    Boolean(selectedWardId || fromDate || toDate || selectedRole || selectedCoverageStatus);

  return (
    <div className="card" style={{ marginBottom: '1rem' }}>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.75rem', alignItems: 'flex-end' }}>
        <div style={{ minWidth: '11rem', flex: 1 }}>
          <label htmlFor="shift-filter-ward" style={{ fontSize: '0.75rem' }}>Ward</label>
          <select
            id="shift-filter-ward"
            value={selectedWardId}
            onChange={(e) => onWardChange(e.target.value)}
          >
            <option value="">All wards</option>
            {wards.map((w) => (
              <option key={w.id} value={w.id}>{w.name}</option>
            ))}
          </select>
        </div>

        <div style={{ minWidth: '9rem' }}>
          <label htmlFor="shift-filter-from" style={{ fontSize: '0.75rem' }}>From date</label>
          <input
            id="shift-filter-from"
            type="date"
            value={fromDate}
            onChange={(e) => onFromDateChange(e.target.value)}
          />
        </div>

        <div style={{ minWidth: '9rem' }}>
          <label htmlFor="shift-filter-to" style={{ fontSize: '0.75rem' }}>To date</label>
          <input
            id="shift-filter-to"
            type="date"
            value={toDate}
            onChange={(e) => onToDateChange(e.target.value)}
          />
        </div>

        <div style={{ minWidth: '10rem' }}>
          <label htmlFor="shift-filter-role" style={{ fontSize: '0.75rem' }}>Required role</label>
          <select
            id="shift-filter-role"
            value={selectedRole}
            onChange={(e) => onRoleChange(e.target.value as StaffRole | '')}
          >
            <option value="">All roles</option>
            {staffRoles.map((r) => (
              <option key={r} value={r}>{staffRoleLabels[r]}</option>
            ))}
          </select>
        </div>

        <div style={{ minWidth: '10rem' }}>
          <label htmlFor="shift-filter-status" style={{ fontSize: '0.75rem' }}>Coverage status</label>
          <select
            id="shift-filter-status"
            value={selectedCoverageStatus}
            onChange={(e) => onCoverageStatusChange(e.target.value as CoverageStatus | '')}
          >
            <option value="">All statuses</option>
            {(Object.keys(coverageStatusLabels) as CoverageStatus[]).map((st) => (
              <option key={st} value={st}>{coverageStatusLabels[st]}</option>
            ))}
          </select>
        </div>

        {hasActiveFilters && (
          <button
            type="button"
            className="secondary small"
            onClick={onReset}
            style={{ alignSelf: 'flex-end', marginBottom: '0.2rem' }}
          >
            Clear filters
          </button>
        )}
      </div>
    </div>
  );
}
