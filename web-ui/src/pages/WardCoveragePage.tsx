import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { getWardCoverageOptions } from '../services/api/generated/@tanstack/react-query.gen';
import type { CoverageStatus, WardCoverageDto } from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { canViewStaff } from '../types/permissions';
import { coverageStatusLabels, coverageStatusTones, staffRoleLabels } from '../types/staff';

export function WardCoveragePage() {
  const session = useSession();
  const role = session?.principal.role;
  const canView = canViewStaff(role);

  const [atTime, setAtTime] = useState<string>('');
  const [appliedAt, setAppliedAt] = useState<string | undefined>(undefined);
  const [statusFilter, setStatusFilter] = useState<CoverageStatus | 'all'>('all');
  const [expandedWardId, setExpandedWardId] = useState<string | null>(null);

  const coverageQuery = useQuery({
    ...getWardCoverageOptions({
      query: appliedAt ? { at: new Date(appliedAt).toISOString() } : undefined,
    }),
    enabled: canView,
  });

  if (!canView) {
    return (
      <>
        <h1>Ward staffing coverage</h1>
        <p className="empty">
          Your role cannot view ward staffing coverage. Hospital administrators and duty managers have access.
        </p>
      </>
    );
  }

  function handleFilterSubmit(e: FormEvent) {
    e.preventDefault();
    setAppliedAt(atTime || undefined);
  }

  function handleResetTime() {
    setAtTime('');
    setAppliedAt(undefined);
  }

  const overview = coverageQuery.data;
  const wards = overview?.wards ?? [];

  // Summary statistics
  const totalWards = wards.length;
  const totalOnDuty = wards.reduce((sum, w) => sum + w.on_duty_count, 0);
  const totalMinimum = wards.reduce((sum, w) => sum + w.minimum_headcount, 0);
  const totalNeeded = wards.reduce((sum, w) => sum + w.headcount_needed, 0);

  const adequateCount = wards.filter((w) => w.status === 'adequate').length;
  const atMinimumCount = wards.filter((w) => w.status === 'at_minimum').length;
  const understaffedCount = wards.filter((w) => w.status === 'understaffed').length;
  const criticalCount = wards.filter((w) => w.status === 'critical').length;

  const filteredWards = wards.filter((w) => {
    if (statusFilter === 'all') return true;
    return w.status === statusFilter;
  });

  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '1rem', marginBottom: '0.5rem' }}>
        <div>
          <h1>Ward staffing coverage</h1>
          <p className="muted">
            Real-time ward staffing levels, headcount requirements, and coverage status.
          </p>
        </div>

        <div className="actions">
          <Link to="/staff" className="secondary button" style={{ textDecoration: 'none' }}>
            Staff directory
          </Link>
          <Link to="/staff/leave-approval" className="secondary button" style={{ textDecoration: 'none' }}>
            Leave requests
          </Link>
          <button
            type="button"
            className="secondary"
            disabled={coverageQuery.isFetching}
            onClick={() => void coverageQuery.refetch()}
          >
            {coverageQuery.isFetching ? 'Refreshing…' : 'Refresh snapshot'}
          </button>
        </div>
      </div>

      {/* Snapshot metadata & time filter */}
      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.75rem' }}>
          <div>
            <h2>Coverage snapshot</h2>
            <p className="muted">
              {overview?.generated_at ? (
                <>
                  Snapshot generated at:{' '}
                  <strong>
                    {new Date(overview.generated_at).toLocaleTimeString(undefined, {
                      hour: '2-digit',
                      minute: '2-digit',
                      second: '2-digit',
                    })}
                  </strong>{' '}
                  ({new Date(overview.generated_at).toLocaleDateString()})
                </>
              ) : (
                'Fetching live coverage data...'
              )}
              {appliedAt && ' · Viewing custom timestamp'}
            </p>
          </div>

          <form onSubmit={handleFilterSubmit} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
            <div>
              <label htmlFor="coverage-at-time" style={{ fontSize: '0.75rem' }}>
                Historical / future time
              </label>
              <input
                id="coverage-at-time"
                type="datetime-local"
                value={atTime}
                onChange={(e) => setAtTime(e.target.value)}
                style={{ fontSize: '0.85rem', padding: '0.35rem 0.5rem' }}
              />
            </div>
            <button type="submit" className="small" disabled={coverageQuery.isFetching}>
              Apply
            </button>
            {appliedAt && (
              <button type="button" className="secondary small" onClick={handleResetTime}>
                Live now
              </button>
            )}
          </form>
        </div>

        {/* Hospital totals stats */}
        <div className="stats" style={{ marginTop: '1rem' }}>
          <div className="stat">
            <div className="value">{totalWards}</div>
            <div className="caption">Monitored wards</div>
          </div>
          <div className="stat">
            <div className="value">{totalOnDuty}</div>
            <div className="caption">Staff on duty</div>
          </div>
          <div className="stat">
            <div className="value">{totalMinimum}</div>
            <div className="caption">Required minimum</div>
          </div>
          <div className={`stat ${totalNeeded > 0 ? 'none' : 'free'}`}>
            <div className="value">{totalNeeded}</div>
            <div className="caption">{totalNeeded > 0 ? 'Staff shortfall' : 'Fully staffed'}</div>
          </div>
        </div>

        {/* Status count pills */}
        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginTop: '0.85rem' }}>
          <span className="badge" style={{ padding: '0.2rem 0.6rem' }}>
            Adequate: {adequateCount}
          </span>
          <span className="badge severity-low" style={{ padding: '0.2rem 0.6rem' }}>
            At minimum: {atMinimumCount}
          </span>
          <span className="badge severity-high" style={{ padding: '0.2rem 0.6rem' }}>
            Understaffed: {understaffedCount}
          </span>
          <span className="badge severity-critical" style={{ padding: '0.2rem 0.6rem' }}>
            Critical: {criticalCount}
          </span>
        </div>
      </div>

      {/* Ward coverage breakdown card */}
      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.75rem', marginBottom: '0.85rem' }}>
          <h2>Ward staffing status ({filteredWards.length})</h2>

          <div style={{ display: 'flex', gap: '0.4rem', alignItems: 'center' }}>
            <label htmlFor="coverage-status-filter" style={{ margin: 0, fontSize: '0.8rem' }}>
              Filter status:
            </label>
            <select
              id="coverage-status-filter"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as CoverageStatus | 'all')}
              style={{ width: 'auto', padding: '0.35rem 0.6rem', fontSize: '0.85rem' }}
            >
              <option value="all">All statuses ({totalWards})</option>
              <option value="adequate">Adequate ({adequateCount})</option>
              <option value="at_minimum">At minimum ({atMinimumCount})</option>
              <option value="understaffed">Understaffed ({understaffedCount})</option>
              <option value="critical">Critical ({criticalCount})</option>
            </select>
          </div>
        </div>

        {coverageQuery.isLoading ? (
          <p className="empty">Loading ward coverage snapshot…</p>
        ) : coverageQuery.isError ? (
          <div className="empty">
            <p style={{ color: 'var(--danger)' }}>Could not load ward coverage information.</p>
            <button
              type="button"
              className="secondary"
              onClick={() => void coverageQuery.refetch()}
            >
              Retry
            </button>
          </div>
        ) : wards.length === 0 ? (
          <p className="empty">No wards are currently monitored for staffing coverage.</p>
        ) : filteredWards.length === 0 ? (
          <div className="empty">
            <p>No wards match the selected status filter &ldquo;{statusFilter}&rdquo;.</p>
            <button
              type="button"
              className="secondary"
              onClick={() => setStatusFilter('all')}
            >
              Show all wards
            </button>
          </div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Ward</th>
                <th>Coverage status</th>
                <th>On duty / Minimum</th>
                <th>Headcount needed</th>
                <th>Coverage ratio</th>
                <th>Roles present</th>
                <th style={{ textAlign: 'right' }}>Details</th>
              </tr>
            </thead>
            <tbody>
              {filteredWards.map((ward) => (
                <WardCoverageRow
                  key={ward.ward_id}
                  ward={ward}
                  isExpanded={expandedWardId === ward.ward_id}
                  onToggleExpand={() =>
                    setExpandedWardId((curr) => (curr === ward.ward_id ? null : ward.ward_id))
                  }
                />
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}

// -------------------------------------------------------------
// Ward Coverage Row Component
// -------------------------------------------------------------

function WardCoverageRow({
  ward,
  isExpanded,
  onToggleExpand,
}: {
  ward: WardCoverageDto;
  isExpanded: boolean;
  onToggleExpand: () => void;
}) {
  const coveragePercent =
    ward.minimum_headcount > 0
      ? Math.round((ward.on_duty_count / ward.minimum_headcount) * 100)
      : null;

  const roleEntries = Object.entries(ward.by_role ?? {});

  return (
    <>
      <tr className={isExpanded ? 'open' : undefined}>
        <td>
          <strong>{ward.ward_name}</strong>
          <br />
          <span className="muted" style={{ fontSize: '0.75rem' }}>
            {ward.current_shift_id ? 'Active shift in progress' : 'No active shift recorded'}
          </span>
        </td>
        <td>
          <span className={coverageStatusTones[ward.status]}>
            {coverageStatusLabels[ward.status] ?? ward.status}
          </span>
        </td>
        <td>
          <strong>{ward.on_duty_count}</strong> / {ward.minimum_headcount}
        </td>
        <td>
          {ward.headcount_needed > 0 ? (
            <span style={{ color: 'var(--danger)', fontWeight: 600 }}>
              +{ward.headcount_needed} needed
            </span>
          ) : (
            <span className="muted">Fulfilled</span>
          )}
        </td>
        <td style={{ minWidth: '7rem' }}>
          {coveragePercent !== null ? (
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.78rem', marginBottom: '0.15rem' }}>
                <span>{coveragePercent}%</span>
              </div>
              <span
                className={`meter ${coveragePercent < 100 ? 'full' : ''}`}
                role="img"
                aria-label={`${coveragePercent}% coverage`}
              >
                <span style={{ width: `${Math.min(100, coveragePercent)}%` }} />
              </span>
            </div>
          ) : (
            <span className="muted">No minimum</span>
          )}
        </td>
        <td>
          {roleEntries.length > 0 ? (
            <div style={{ display: 'flex', gap: '0.3rem', flexWrap: 'wrap' }}>
              {roleEntries.map(([roleKey, count]) => (
                <span
                  key={roleKey}
                  className="badge retired"
                  style={{ fontSize: '0.72rem', padding: '0.1rem 0.4rem' }}
                >
                  {count} {staffRoleLabels[roleKey as keyof typeof staffRoleLabels] ?? roleKey}
                </span>
              ))}
            </div>
          ) : (
            <span className="muted">None</span>
          )}
        </td>
        <td style={{ textAlign: 'right' }}>
          <button
            type="button"
            className="secondary small"
            onClick={onToggleExpand}
          >
            {isExpanded ? 'Hide' : 'Breakdown'}
          </button>
        </td>
      </tr>

      {/* Expandable breakdown drawer */}
      {isExpanded && (
        <tr className="drawer">
          <td colSpan={7} style={{ background: 'var(--canvas)', padding: '1rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '1rem' }}>
              <div>
                <h3 style={{ margin: '0 0 0.4rem' }}>{ward.ward_name} — Staffing breakdown</h3>
                <p className="muted" style={{ margin: '0 0 0.6rem', fontSize: '0.82rem' }}>
                  Current on-duty headcount vs ward staffing requirements.
                </p>

                <div style={{ display: 'flex', gap: '1.5rem', flexWrap: 'wrap', fontSize: '0.85rem' }}>
                  <div>
                    <span className="muted">Status:</span>{' '}
                    <span className={coverageStatusTones[ward.status]}>
                      {coverageStatusLabels[ward.status]}
                    </span>
                  </div>
                  <div>
                    <span className="muted">On-duty headcount:</span>{' '}
                    <strong>{ward.on_duty_count}</strong>
                  </div>
                  <div>
                    <span className="muted">Minimum required:</span>{' '}
                    <strong>{ward.minimum_headcount}</strong>
                  </div>
                  <div>
                    <span className="muted">Shortfall:</span>{' '}
                    <strong>{ward.headcount_needed}</strong>
                  </div>
                  <div>
                    <span className="muted">Shift ID:</span>{' '}
                    <code>{ward.current_shift_id ?? 'None'}</code>
                  </div>
                </div>
              </div>

              <button
                type="button"
                className="secondary small"
                onClick={onToggleExpand}
              >
                Close breakdown
              </button>
            </div>

            <div style={{ marginTop: '0.85rem' }}>
              <h4 style={{ margin: '0 0 0.35rem', fontSize: '0.82rem', textTransform: 'uppercase', letterSpacing: '0.04em', color: 'var(--ink-soft)' }}>
                On-duty staff by role
              </h4>
              {roleEntries.length > 0 ? (
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(11rem, 1fr))', gap: '0.5rem' }}>
                  {roleEntries.map(([roleKey, count]) => (
                    <div
                      key={roleKey}
                      style={{
                        background: 'var(--surface)',
                        border: '1px solid var(--line)',
                        borderRadius: '6px',
                        padding: '0.5rem 0.75rem',
                      }}
                    >
                      <div style={{ fontSize: '1.1rem', fontWeight: 700 }}>{count}</div>
                      <div className="muted" style={{ fontSize: '0.78rem' }}>
                        {staffRoleLabels[roleKey as keyof typeof staffRoleLabels] ?? roleKey}
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="muted" style={{ fontStyle: 'italic', margin: 0 }}>
                  No staff members currently clocked in or allocated to this ward.
                </p>
              )}
            </div>
          </td>
        </tr>
      )}
    </>
  );
}
