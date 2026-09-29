import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  getCoverageReportOptions,
  getLeaveReportOptions,
  getStaffAgentPerformanceReportOptions,
  listWardsOptions,
} from '../../services/api/generated/@tanstack/react-query.gen';
import type {
  CoverageReportRow,
  CoverageReportTotals,
  LeaveReportRow,
  StaffAgentPerformanceReport,
  Ward,
} from '../../services/api/generated';
import { useSession } from '../../services/auth/useSession';
import { canManageStaff, canViewStaff } from '../../types/permissions';
import { leaveTypeLabels } from '../../types/staff';

function defaultRange(daysBack = 7) {
  const today = new Date();
  const past = new Date();
  past.setDate(past.getDate() - daysBack);
  return {
    from: past.toISOString().slice(0, 10),
    to: today.toISOString().slice(0, 10),
  };
}

export function StaffReportsPage() {
  const session = useSession();
  const role = session?.principal.role;
  const canView = canViewStaff(role);
  const canManage = canManageStaff(role);

  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = searchParams.get('tab') || 'coverage';

  const setTab = (t: string) => {
    setSearchParams({ tab: t });
  };

  if (!canView) {
    return (
      <>
        <h1>Staff reports</h1>
        <p className="empty">
          Your role cannot view staff reports. Hospital administrators and duty managers have access.
        </p>
      </>
    );
  }

  return (
    <>
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'flex-start',
          flexWrap: 'wrap',
          gap: '1rem',
          marginBottom: '0.5rem',
        }}
      >
        <div>
          <h1>Staff reports</h1>
          <p className="muted">
            Operational analytics, ward staffing coverage over time, leave trends, and allocation agent performance.
            {!canManage && ' (Duty Manager: coverage reports only)'}
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
          <Link to="/staff/roster-proposals" className="secondary button" style={{ textDecoration: 'none' }}>
            Roster proposals
          </Link>
        </div>
      </div>

      {/* Report tab selector */}
      <div
        style={{
          display: 'flex',
          gap: '0.5rem',
          marginBottom: '1.25rem',
          borderBottom: '1px solid var(--line)',
          paddingBottom: '0.5rem',
        }}
      >
        <button
          type="button"
          className={activeTab === 'coverage' ? '' : 'secondary'}
          onClick={() => setTab('coverage')}
          style={{ fontWeight: activeTab === 'coverage' ? 600 : 400 }}
        >
          Ward coverage report
        </button>
        <button
          type="button"
          className={activeTab === 'leave' ? '' : 'secondary'}
          onClick={() => setTab('leave')}
          style={{ fontWeight: activeTab === 'leave' ? 600 : 400 }}
        >
          Staff leave report
        </button>
        <button
          type="button"
          className={activeTab === 'agent' ? '' : 'secondary'}
          onClick={() => setTab('agent')}
          style={{ fontWeight: activeTab === 'agent' ? 600 : 400 }}
        >
          Staff agent performance
        </button>
      </div>

      {activeTab === 'coverage' && <CoverageReportView canView={canView} />}
      {activeTab === 'leave' && <LeaveReportView canManage={canManage} />}
      {activeTab === 'agent' && <AgentPerformanceReportView canManage={canManage} />}
    </>
  );
}

// ----------------------------------------------------------------------
// 1. Coverage Report Component
// ----------------------------------------------------------------------

function CoverageReportView({ canView }: { canView: boolean }) {
  const initial = defaultRange(7);
  const [draftFrom, setDraftFrom] = useState(initial.from);
  const [draftTo, setDraftTo] = useState(initial.to);
  const [draftWardId, setDraftWardId] = useState('');

  const [appliedFrom, setAppliedFrom] = useState(initial.from);
  const [appliedTo, setAppliedTo] = useState(initial.to);
  const [appliedWardId, setAppliedWardId] = useState('');

  const wardsQuery = useQuery(listWardsOptions());
  const wards: Ward[] = wardsQuery.data ?? [];

  const coverageQuery = useQuery({
    ...getCoverageReportOptions({
      query: {
        from: appliedFrom,
        to: appliedTo,
        ...(appliedWardId ? { wardId: appliedWardId } : {}),
      },
    }),
    enabled: canView && Boolean(appliedFrom) && Boolean(appliedTo),
  });

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (draftFrom && draftTo && draftFrom <= draftTo) {
      setAppliedFrom(draftFrom);
      setAppliedTo(draftTo);
      setAppliedWardId(draftWardId);
    }
  }

  function applyPreset(days: number) {
    const range = defaultRange(days);
    setDraftFrom(range.from);
    setDraftTo(range.to);
    setAppliedFrom(range.from);
    setAppliedTo(range.to);
  }

  const report = coverageQuery.data;
  const rows: CoverageReportRow[] = report?.rows ?? [];
  const totals: CoverageReportTotals | undefined = report?.totals;

  return (
    <div>
      {/* Filter Card */}
      <div className="card" style={{ marginBottom: '1.25rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem', marginBottom: '0.85rem' }}>
          <h2 style={{ margin: 0 }}>Coverage report parameters</h2>
          <div style={{ display: 'flex', gap: '0.4rem' }}>
            <button type="button" className="secondary small" onClick={() => applyPreset(7)}>
              Last 7 days
            </button>
            <button type="button" className="secondary small" onClick={() => applyPreset(14)}>
              Last 14 days
            </button>
            <button type="button" className="secondary small" onClick={() => applyPreset(30)}>
              Last 30 days
            </button>
          </div>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="row">
            <div>
              <label htmlFor="cov-from">From date *</label>
              <input
                id="cov-from"
                type="date"
                required
                value={draftFrom}
                onChange={(e) => setDraftFrom(e.target.value)}
              />
            </div>
            <div>
              <label htmlFor="cov-to">To date *</label>
              <input
                id="cov-to"
                type="date"
                required
                value={draftTo}
                onChange={(e) => setDraftTo(e.target.value)}
              />
            </div>
            <div>
              <label htmlFor="cov-ward">Ward filter</label>
              <select
                id="cov-ward"
                value={draftWardId}
                onChange={(e) => setDraftWardId(e.target.value)}
              >
                <option value="">All hospital wards</option>
                {wards.map((w) => (
                  <option key={w.id} value={w.id}>
                    {w.name}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="actions" style={{ marginTop: '1rem', justifyContent: 'flex-end' }}>
            <button
              type="submit"
              disabled={!draftFrom || !draftTo || draftFrom > draftTo || coverageQuery.isFetching}
            >
              {coverageQuery.isFetching ? 'Loading report…' : 'Generate coverage report'}
            </button>
          </div>
        </form>
      </div>

      {/* Query state handling */}
      {coverageQuery.isLoading ? (
        <p className="empty">Loading ward coverage analytics…</p>
      ) : coverageQuery.isError ? (
        <div className="empty">
          <p style={{ color: 'var(--danger)' }}>Failed to load coverage report.</p>
          <button type="button" className="secondary" onClick={() => void coverageQuery.refetch()}>
            Retry
          </button>
        </div>
      ) : (
        <>
          {/* Summary KPIs */}
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
              gap: '1rem',
              marginBottom: '1.25rem',
            }}
          >
            <div className="card" style={{ margin: 0, textAlign: 'center' }}>
              <div className="muted" style={{ fontSize: '0.85rem' }}>Total scheduled shifts</div>
              <div style={{ fontSize: '1.8rem', fontWeight: 700, marginTop: '0.2rem' }}>
                {totals?.shifts_total ?? 0}
              </div>
              <span className="muted" style={{ fontSize: '0.75rem' }}>Across filtered wards</span>
            </div>

            <div className="card" style={{ margin: 0, textAlign: 'center' }}>
              <div className="muted" style={{ fontSize: '0.85rem' }}>Understaffed shifts</div>
              <div
                style={{
                  fontSize: '1.8rem',
                  fontWeight: 700,
                  marginTop: '0.2rem',
                  color: (totals?.shifts_understaffed ?? 0) > 0 ? 'var(--danger)' : 'inherit',
                }}
              >
                {totals?.shifts_understaffed ?? 0}
              </div>
              <span className="muted" style={{ fontSize: '0.75rem' }}>Shifts below target headcount</span>
            </div>

            <div className="card" style={{ margin: 0, textAlign: 'center' }}>
              <div className="muted" style={{ fontSize: '0.85rem' }}>Hours below minimum</div>
              <div
                style={{
                  fontSize: '1.8rem',
                  fontWeight: 700,
                  marginTop: '0.2rem',
                  color: (totals?.hours_below_minimum ?? 0) > 0 ? 'var(--danger)' : 'inherit',
                }}
              >
                {(totals?.hours_below_minimum ?? 0).toFixed(1)} hrs
              </div>
              <span className="muted" style={{ fontSize: '0.75rem' }}>Critical compliance shortfall</span>
            </div>

            <div className="card" style={{ margin: 0, textAlign: 'center' }}>
              <div className="muted" style={{ fontSize: '0.85rem' }}>Overall fill rate</div>
              <div
                style={{
                  fontSize: '1.8rem',
                  fontWeight: 700,
                  marginTop: '0.2rem',
                  color:
                    (totals?.fill_rate ?? 0) >= 0.95
                      ? 'var(--accent, #16a34a)'
                      : (totals?.fill_rate ?? 0) >= 0.8
                      ? '#d97706'
                      : 'var(--danger)',
                }}
              >
                {totals?.fill_rate !== undefined && totals?.fill_rate !== null
                  ? `${(totals.fill_rate * 100).toFixed(1)}%`
                  : '0.0%'}
              </div>
              <span className="muted" style={{ fontSize: '0.75rem' }}>Staffed vs required hours</span>
            </div>
          </div>

          {/* Report Data Table */}
          <div className="card">
            <h2>Detailed ward coverage records ({rows.length})</h2>
            <p className="muted" style={{ marginBottom: '1rem' }}>
              Daily ward coverage breakdown covering {appliedFrom} to {appliedTo}.
            </p>

            {rows.length === 0 ? (
              <p className="empty">No coverage data recorded for the selected period and ward.</p>
            ) : (
              <table>
                <thead>
                  <tr>
                    <th>Date</th>
                    <th>Ward</th>
                    <th>Total shifts</th>
                    <th>Understaffed</th>
                    <th>Hours below minimum</th>
                    <th>Fill rate</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r, index) => {
                    const fill = r.fill_rate ?? 0;
                    const under = r.shifts_understaffed ?? 0;
                    const belowHours = r.hours_below_minimum ?? 0;

                    let statusBadge = <span className="badge">Adequate</span>;
                    if (belowHours > 0 || under > 0) {
                      statusBadge = <span className="badge severity-critical">Understaffed</span>;
                    } else if (fill < 0.95) {
                      statusBadge = <span className="badge severity-medium">At minimum</span>;
                    }

                    return (
                      <tr key={`${r.ward_id ?? 'ward'}-${r.date ?? index}`}>
                        <td>
                          <strong>{r.date || '—'}</strong>
                        </td>
                        <td>{r.ward_name || <span className="muted">All wards</span>}</td>
                        <td>{r.shifts_total ?? 0}</td>
                        <td>
                          {under > 0 ? (
                            <span style={{ color: 'var(--danger)', fontWeight: 600 }}>{under}</span>
                          ) : (
                            <span>0</span>
                          )}
                        </td>
                        <td>
                          {belowHours > 0 ? (
                            <span style={{ color: 'var(--danger)', fontWeight: 600 }}>
                              {belowHours.toFixed(1)} hrs
                            </span>
                          ) : (
                            <span className="muted">0.0 hrs</span>
                          )}
                        </td>
                        <td>
                          <strong>{(fill * 100).toFixed(1)}%</strong>
                        </td>
                        <td>{statusBadge}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
        </>
      )}
    </div>
  );
}

// ----------------------------------------------------------------------
// 2. Leave Report Component
// ----------------------------------------------------------------------

function LeaveReportView({ canManage }: { canManage: boolean }) {
  const initial = defaultRange(30);
  const [draftFrom, setDraftFrom] = useState(initial.from);
  const [draftTo, setDraftTo] = useState(initial.to);
  const [draftGroupBy, setDraftGroupBy] = useState<'type' | 'staff' | 'ward' | 'month'>('type');

  const [appliedFrom, setAppliedFrom] = useState(initial.from);
  const [appliedTo, setAppliedTo] = useState(initial.to);
  const [appliedGroupBy, setAppliedGroupBy] = useState<'type' | 'staff' | 'ward' | 'month'>('type');

  const leaveQuery = useQuery({
    ...getLeaveReportOptions({
      query: {
        from: appliedFrom,
        to: appliedTo,
        groupBy: appliedGroupBy,
      },
    }),
    enabled: canManage && Boolean(appliedFrom) && Boolean(appliedTo),
  });

  if (!canManage) {
    return (
      <div className="card">
        <h2>Staff leave report</h2>
        <div className="stub-note" style={{ borderColor: 'var(--ink-soft)' }}>
          <strong>Hospital Administrator access required.</strong>
          <p className="muted" style={{ margin: '0.25rem 0 0 0' }}>
            Staff leave reporting contains sensitive hospital-wide personnel data and is restricted to Hospital Administrators.
          </p>
        </div>
      </div>
    );
  }

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (draftFrom && draftTo && draftFrom <= draftTo) {
      setAppliedFrom(draftFrom);
      setAppliedTo(draftTo);
      setAppliedGroupBy(draftGroupBy);
    }
  }

  function applyPreset(days: number) {
    const range = defaultRange(days);
    setDraftFrom(range.from);
    setDraftTo(range.to);
    setAppliedFrom(range.from);
    setAppliedTo(range.to);
  }

  const report = leaveQuery.data;
  const rows: LeaveReportRow[] = report?.rows ?? [];

  const totalApproved = rows.reduce((sum, r) => sum + (r.approved_days ?? 0), 0);
  const totalPending = rows.reduce((sum, r) => sum + (r.pending_days ?? 0), 0);
  const totalSick = rows.reduce((sum, r) => sum + (r.sick_days ?? 0), 0);
  const totalRejected = rows.reduce((sum, r) => sum + (r.rejected_count ?? 0), 0);

  const groupHeaders: Record<string, string> = {
    type: 'Leave type',
    staff: 'Staff member / Key',
    ward: 'Ward / Unit',
    month: 'Month',
  };

  return (
    <div>
      {/* Filter Card */}
      <div className="card" style={{ marginBottom: '1.25rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem', marginBottom: '0.85rem' }}>
          <h2 style={{ margin: 0 }}>Staff leave report parameters</h2>
          <div style={{ display: 'flex', gap: '0.4rem' }}>
            <button type="button" className="secondary small" onClick={() => applyPreset(30)}>
              Last 30 days
            </button>
            <button type="button" className="secondary small" onClick={() => applyPreset(60)}>
              Last 60 days
            </button>
            <button type="button" className="secondary small" onClick={() => applyPreset(90)}>
              Last 90 days
            </button>
          </div>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="row">
            <div>
              <label htmlFor="leave-from">From date *</label>
              <input
                id="leave-from"
                type="date"
                required
                value={draftFrom}
                onChange={(e) => setDraftFrom(e.target.value)}
              />
            </div>
            <div>
              <label htmlFor="leave-to">To date *</label>
              <input
                id="leave-to"
                type="date"
                required
                value={draftTo}
                onChange={(e) => setDraftTo(e.target.value)}
              />
            </div>
            <div>
              <label htmlFor="leave-group">Group results by</label>
              <select
                id="leave-group"
                value={draftGroupBy}
                onChange={(e) => setDraftGroupBy(e.target.value as 'type' | 'staff' | 'ward' | 'month')}
              >
                <option value="type">Leave type (Annual, Sick, Casual, etc.)</option>
                <option value="staff">Staff member</option>
                <option value="ward">Ward</option>
                <option value="month">Month</option>
              </select>
            </div>
          </div>

          <div className="actions" style={{ marginTop: '1rem', justifyContent: 'flex-end' }}>
            <button
              type="submit"
              disabled={!draftFrom || !draftTo || draftFrom > draftTo || leaveQuery.isFetching}
            >
              {leaveQuery.isFetching ? 'Loading report…' : 'Generate leave report'}
            </button>
          </div>
        </form>
      </div>

      {leaveQuery.isLoading ? (
        <p className="empty">Loading staff leave analytics…</p>
      ) : leaveQuery.isError ? (
        <div className="empty">
          <p style={{ color: 'var(--danger)' }}>Failed to load staff leave report.</p>
          <button type="button" className="secondary" onClick={() => void leaveQuery.refetch()}>
            Retry
          </button>
        </div>
      ) : (
        <>
          {/* Summary KPIs */}
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
              gap: '1rem',
              marginBottom: '1.25rem',
            }}
          >
            <div className="card" style={{ margin: 0, textAlign: 'center' }}>
              <div className="muted" style={{ fontSize: '0.85rem' }}>Approved leave days</div>
              <div style={{ fontSize: '1.8rem', fontWeight: 700, marginTop: '0.2rem', color: 'var(--accent, #16a34a)' }}>
                {totalApproved}
              </div>
              <span className="muted" style={{ fontSize: '0.75rem' }}>Days taken or approved</span>
            </div>

            <div className="card" style={{ margin: 0, textAlign: 'center' }}>
              <div className="muted" style={{ fontSize: '0.85rem' }}>Pending leave days</div>
              <div style={{ fontSize: '1.8rem', fontWeight: 700, marginTop: '0.2rem', color: '#d97706' }}>
                {totalPending}
              </div>
              <span className="muted" style={{ fontSize: '0.75rem' }}>Awaiting managerial decision</span>
            </div>

            <div className="card" style={{ margin: 0, textAlign: 'center' }}>
              <div className="muted" style={{ fontSize: '0.85rem' }}>Sick leave days</div>
              <div style={{ fontSize: '1.8rem', fontWeight: 700, marginTop: '0.2rem' }}>
                {totalSick}
              </div>
              <span className="muted" style={{ fontSize: '0.75rem' }}>Medical & emergency leave</span>
            </div>

            <div className="card" style={{ margin: 0, textAlign: 'center' }}>
              <div className="muted" style={{ fontSize: '0.85rem' }}>Rejected requests</div>
              <div style={{ fontSize: '1.8rem', fontWeight: 700, marginTop: '0.2rem', color: totalRejected > 0 ? 'var(--danger)' : 'inherit' }}>
                {totalRejected}
              </div>
              <span className="muted" style={{ fontSize: '0.75rem' }}>Requests denied due to coverage</span>
            </div>
          </div>

          {/* Table */}
          <div className="card">
            <h2>
              Leave breakdown by {appliedGroupBy} ({rows.length})
            </h2>
            <p className="muted" style={{ marginBottom: '1rem' }}>
              Covering {appliedFrom} to {appliedTo} grouped by {appliedGroupBy}.
            </p>

            {rows.length === 0 ? (
              <p className="empty">No leave records recorded for the selected period.</p>
            ) : (
              <table>
                <thead>
                  <tr>
                    <th>{groupHeaders[appliedGroupBy] ?? 'Category'}</th>
                    <th>Approved days</th>
                    <th>Pending days</th>
                    <th>Sick days</th>
                    <th>Rejected requests</th>
                    <th>Total days requested</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r, idx) => {
                    const keyLabel =
                      appliedGroupBy === 'type' && r.key
                        ? leaveTypeLabels[r.key as keyof typeof leaveTypeLabels] ?? r.key
                        : r.key || '—';
                    const rowTotal = (r.approved_days ?? 0) + (r.pending_days ?? 0);

                    return (
                      <tr key={`${r.key ?? idx}`}>
                        <td>
                          <strong>{keyLabel}</strong>
                        </td>
                        <td>
                          <span style={{ color: (r.approved_days ?? 0) > 0 ? 'var(--accent, #16a34a)' : 'inherit' }}>
                            {r.approved_days ?? 0}
                          </span>
                        </td>
                        <td>
                          {(r.pending_days ?? 0) > 0 ? (
                            <span style={{ color: '#d97706', fontWeight: 600 }}>{r.pending_days}</span>
                          ) : (
                            <span>0</span>
                          )}
                        </td>
                        <td>{r.sick_days ?? 0}</td>
                        <td>
                          {(r.rejected_count ?? 0) > 0 ? (
                            <span style={{ color: 'var(--danger)' }}>{r.rejected_count}</span>
                          ) : (
                            <span className="muted">0</span>
                          )}
                        </td>
                        <td>
                          <strong>{rowTotal}</strong>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
        </>
      )}
    </div>
  );
}

// ----------------------------------------------------------------------
// 3. Staff Agent Performance Report Component
// ----------------------------------------------------------------------

function AgentPerformanceReportView({ canManage }: { canManage: boolean }) {
  const initial = defaultRange(14);
  const [draftFrom, setDraftFrom] = useState(initial.from);
  const [draftTo, setDraftTo] = useState(initial.to);

  const [appliedFrom, setAppliedFrom] = useState(initial.from);
  const [appliedTo, setAppliedTo] = useState(initial.to);

  const agentQuery = useQuery({
    ...getStaffAgentPerformanceReportOptions({
      query: {
        from: appliedFrom,
        to: appliedTo,
      },
    }),
    enabled: canManage && Boolean(appliedFrom) && Boolean(appliedTo),
  });

  if (!canManage) {
    return (
      <div className="card">
        <h2>Staff agent performance report</h2>
        <div className="stub-note" style={{ borderColor: 'var(--ink-soft)' }}>
          <strong>Hospital Administrator access required.</strong>
          <p className="muted" style={{ margin: '0.25rem 0 0 0' }}>
            Agentic AI evaluation metrics and deterministic verification statistics are restricted to Hospital Administrators.
          </p>
        </div>
      </div>
    );
  }

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (draftFrom && draftTo && draftFrom <= draftTo) {
      setAppliedFrom(draftFrom);
      setAppliedTo(draftTo);
    }
  }

  function applyPreset(days: number) {
    const range = defaultRange(days);
    setDraftFrom(range.from);
    setDraftTo(range.to);
    setAppliedFrom(range.from);
    setAppliedTo(range.to);
  }

  const data: StaffAgentPerformanceReport | undefined = agentQuery.data;

  const totalDecisions = (data?.approved ?? 0) + (data?.rejected ?? 0);
  const approvalRate =
    totalDecisions > 0 ? ((data?.approved ?? 0) / totalDecisions) * 100 : null;

  const rejectionEntries = Object.entries(data?.rejection_reasons ?? {});
  const totalRejectionsInMap = rejectionEntries.reduce((sum, [, count]) => sum + count, 0);

  return (
    <div>
      {/* Filter Card */}
      <div className="card" style={{ marginBottom: '1.25rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem', marginBottom: '0.85rem' }}>
          <div>
            <h2 style={{ margin: 0 }}>Agent performance evaluation period</h2>
            <p className="muted" style={{ margin: '0.25rem 0 0 0' }}>
              Evaluation metrics for the Staff Allocation Agent (Assignment 1 §12 benchmark).
            </p>
          </div>
          <div style={{ display: 'flex', gap: '0.4rem' }}>
            <button type="button" className="secondary small" onClick={() => applyPreset(7)}>
              Last 7 days
            </button>
            <button type="button" className="secondary small" onClick={() => applyPreset(14)}>
              Last 14 days
            </button>
            <button type="button" className="secondary small" onClick={() => applyPreset(30)}>
              Last 30 days
            </button>
          </div>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="row">
            <div>
              <label htmlFor="agent-from">From date *</label>
              <input
                id="agent-from"
                type="date"
                required
                value={draftFrom}
                onChange={(e) => setDraftFrom(e.target.value)}
              />
            </div>
            <div>
              <label htmlFor="agent-to">To date *</label>
              <input
                id="agent-to"
                type="date"
                required
                value={draftTo}
                onChange={(e) => setDraftTo(e.target.value)}
              />
            </div>
          </div>

          <div className="actions" style={{ marginTop: '1rem', justifyContent: 'flex-end' }}>
            <button
              type="submit"
              disabled={!draftFrom || !draftTo || draftFrom > draftTo || agentQuery.isFetching}
            >
              {agentQuery.isFetching ? 'Loading analytics…' : 'Generate agent report'}
            </button>
          </div>
        </form>
      </div>

      {agentQuery.isLoading ? (
        <p className="empty">Loading Staff Allocation Agent metrics…</p>
      ) : agentQuery.isError ? (
        <div className="empty">
          <p style={{ color: 'var(--danger)' }}>Failed to load agent performance report.</p>
          <button type="button" className="secondary" onClick={() => void agentQuery.refetch()}>
            Retry
          </button>
        </div>
      ) : !data ? (
        <p className="empty">No agent performance data recorded for this date range.</p>
      ) : (
        <>
          {/* Key Executive KPIs */}
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
              gap: '1rem',
              marginBottom: '1.25rem',
            }}
          >
            <div className="card" style={{ margin: 0, textAlign: 'center' }}>
              <div className="muted" style={{ fontSize: '0.85rem' }}>Proposals raised</div>
              <div style={{ fontSize: '1.8rem', fontWeight: 700, marginTop: '0.2rem' }}>
                {data.proposals_raised ?? 0}
              </div>
              <span className="muted" style={{ fontSize: '0.75rem' }}>
                {data.proposals_auto_triggered ?? 0} triggered automatically
              </span>
            </div>

            <div className="card" style={{ margin: 0, textAlign: 'center' }}>
              <div className="muted" style={{ fontSize: '0.85rem' }}>Human approval rate</div>
              <div
                style={{
                  fontSize: '1.8rem',
                  fontWeight: 700,
                  marginTop: '0.2rem',
                  color:
                    approvalRate !== null && approvalRate >= 80
                      ? 'var(--accent, #16a34a)'
                      : approvalRate !== null && approvalRate >= 60
                      ? '#d97706'
                      : 'inherit',
                }}
              >
                {approvalRate !== null ? `${approvalRate.toFixed(1)}%` : '—'}
              </div>
              <span className="muted" style={{ fontSize: '0.75rem' }}>
                {data.approved ?? 0} approved / {data.rejected ?? 0} rejected
              </span>
            </div>

            <div className="card" style={{ margin: 0, textAlign: 'center' }}>
              <div className="muted" style={{ fontSize: '0.85rem' }}>Validation failure rate</div>
              <div
                style={{
                  fontSize: '1.8rem',
                  fontWeight: 700,
                  marginTop: '0.2rem',
                  color: (data.validation_failure_rate ?? 0) > 0.15 ? 'var(--danger)' : 'inherit',
                }}
              >
                {data.validation_failure_rate !== undefined && data.validation_failure_rate !== null
                  ? `${(data.validation_failure_rate * 100).toFixed(1)}%`
                  : '0.0%'}
              </div>
              <span className="muted" style={{ fontSize: '0.75rem' }}>
                Proposals caught by C# validation
              </span>
            </div>

            <div className="card" style={{ margin: 0, textAlign: 'center' }}>
              <div className="muted" style={{ fontSize: '0.85rem' }}>Median time to fill gap</div>
              <div style={{ fontSize: '1.8rem', fontWeight: 700, marginTop: '0.2rem', color: 'var(--accent)' }}>
                {data.median_minutes_gap_to_fill !== undefined && data.median_minutes_gap_to_fill !== null
                  ? `${data.median_minutes_gap_to_fill}m`
                  : '—'}
              </div>
              <span className="muted" style={{ fontSize: '0.75rem' }}>Gap detected to allocation</span>
            </div>
          </div>

          {/* Operational Metrics Breakdown */}
          <div className="card" style={{ marginBottom: '1.25rem' }}>
            <h2>Agent operational verification breakdown</h2>
            <p className="muted" style={{ marginBottom: '1rem' }}>
              Deterministic validation and human-in-the-loop decision metrics.
            </p>

            <dl className="detail-grid">
              <div>
                <dt>Proposals raised</dt>
                <dd><strong>{data.proposals_raised ?? 0}</strong></dd>
              </div>
              <div>
                <dt>Auto-triggered by leave</dt>
                <dd>{data.proposals_auto_triggered ?? 0}</dd>
              </div>
              <div>
                <dt>Approved by manager</dt>
                <dd style={{ color: 'var(--accent, #16a34a)' }}>
                  <strong>{data.approved ?? 0}</strong>
                </dd>
              </div>
              <div>
                <dt>Rejected by manager</dt>
                <dd style={{ color: (data.rejected ?? 0) > 0 ? 'var(--danger)' : 'inherit' }}>
                  <strong>{data.rejected ?? 0}</strong>
                </dd>
              </div>
              <div>
                <dt>Revisions requested</dt>
                <dd>{data.revision_requested ?? 0}</dd>
              </div>
              <div>
                <dt>Failed safely</dt>
                <dd>{data.failed_safely ?? 0}</dd>
              </div>
              <div>
                <dt>Cascading swaps resolved</dt>
                <dd>{data.cascading_swaps ?? 0}</dd>
              </div>
              <div>
                <dt>Median gap fill time</dt>
                <dd>
                  {data.median_minutes_gap_to_fill !== undefined && data.median_minutes_gap_to_fill !== null
                    ? `${data.median_minutes_gap_to_fill} minutes`
                    : '—'}
                </dd>
              </div>
            </dl>
          </div>

          {/* Rejection Reasons Analysis */}
          <div className="card">
            <h2>Rejection reasons analysis</h2>
            <p className="muted" style={{ marginBottom: '1rem' }}>
              Why managers rejected agent recommendations (feedback for model adjustment).
            </p>

            {rejectionEntries.length === 0 ? (
              <p className="muted" style={{ fontStyle: 'italic' }}>
                No proposal rejections recorded in this period.
              </p>
            ) : (
              <table>
                <thead>
                  <tr>
                    <th>Rejection rationale</th>
                    <th>Incident count</th>
                    <th>Share of rejections</th>
                  </tr>
                </thead>
                <tbody>
                  {rejectionEntries.map(([reason, count]) => {
                    const share =
                      totalRejectionsInMap > 0 ? (count / totalRejectionsInMap) * 100 : 0;
                    return (
                      <tr key={reason}>
                        <td>
                          <strong>{reason.replace(/_/g, ' ')}</strong>
                        </td>
                        <td>{count}</td>
                        <td>
                          <span>{share.toFixed(1)}%</span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
        </>
      )}
    </div>
  );
}
