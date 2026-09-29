import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  getWardCoverageOptions,
  getWardStaffingRulesOptions,
  getWardStaffingRulesQueryKey,
  listSkillsOptions,
  setWardStaffingRulesMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type {
  CoverageStatus,
  SkillDto,
  StaffRole,
  WardCoverageDto,
  WardStaffingRuleDto,
  WardStaffingRuleInput,
} from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { canManageStaff, canViewStaff } from '../types/permissions';
import {
  coverageStatusLabels,
  coverageStatusTones,
  staffRoleLabels,
  staffRoles,
} from '../types/staff';
import { ActionDialog } from '../components/ui/action-dialog';

export function WardCoveragePage() {
  const session = useSession();
  const role = session?.principal.role;
  const canView = canViewStaff(role);
  const canManage = canManageStaff(role);

  const [atTime, setAtTime] = useState<string>('');
  const [appliedAt, setAppliedAt] = useState<string | undefined>(undefined);
  const [statusFilter, setStatusFilter] = useState<CoverageStatus | 'all'>('all');
  const [expandedWardId, setExpandedWardId] = useState<string | null>(null);
  const [selectedWardForRules, setSelectedWardForRules] = useState<WardCoverageDto | null>(null);

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
          <Link to="/staff/shifts" className="secondary button" style={{ textDecoration: 'none' }}>
            Shifts & roster
          </Link>
          <Link to="/staff/leave-approval" className="secondary button" style={{ textDecoration: 'none' }}>
            Leave requests
          </Link>
          <Link to="/staff/roster-proposals" className="secondary button" style={{ textDecoration: 'none' }}>
            Roster proposals
          </Link>
          <Link to="/staff/reports" className="secondary button" style={{ textDecoration: 'none' }}>
            Staff reports
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
                <th style={{ textAlign: 'right' }}>Actions</th>
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
                  canManage={canManage}
                  onConfigureRules={(w) => setSelectedWardForRules(w)}
                />
              ))}
            </tbody>
          </table>
        )}
      </div>

      <ConfigureWardStaffingRulesDialog
        ward={selectedWardForRules}
        isOpen={selectedWardForRules !== null}
        onClose={() => setSelectedWardForRules(null)}
      />
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
  canManage,
  onConfigureRules,
}: {
  ward: WardCoverageDto;
  isExpanded: boolean;
  onToggleExpand: () => void;
  canManage: boolean;
  onConfigureRules: (ward: WardCoverageDto) => void;
}) {
  const coveragePercent =
    ward.minimum_headcount > 0
      ? Math.round((ward.on_duty_count / ward.minimum_headcount) * 100)
      : null;

  const roleEntries = Object.entries(ward.by_role ?? {});

  const rulesQuery = useQuery({
    ...getWardStaffingRulesOptions({ path: { wardId: ward.ward_id } }),
    enabled: isExpanded,
  });

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
        <td style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
          <div style={{ display: 'inline-flex', gap: '0.4rem', justifyContent: 'flex-end' }}>
            <button
              type="button"
              className="secondary small"
              onClick={onToggleExpand}
            >
              {isExpanded ? 'Hide' : 'Breakdown'}
            </button>
            {canManage && (
              <button
                type="button"
                className="secondary small"
                onClick={() => onConfigureRules(ward)}
              >
                Configure rules
              </button>
            )}
          </div>
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

            {/* Minimum Staffing Policy Rules section */}
            <div style={{ marginTop: '1.25rem', paddingTop: '1rem', borderTop: '1px solid var(--line)' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem', marginBottom: '0.6rem' }}>
                <div>
                  <h4 style={{ margin: '0 0 0.2rem', fontSize: '0.85rem', textTransform: 'uppercase', letterSpacing: '0.04em', color: 'var(--ink-soft)' }}>
                    Minimum staffing policy rules ({rulesQuery.data?.length ?? 0})
                  </h4>
                  <p className="muted" style={{ margin: 0, fontSize: '0.8rem' }}>
                    Mandatory minimum headcount and qualifications required for shifts on this ward.
                  </p>
                </div>

                {canManage && (
                  <button
                    type="button"
                    className="secondary small"
                    onClick={() => onConfigureRules(ward)}
                  >
                    Configure rules
                  </button>
                )}
              </div>

              {rulesQuery.isLoading ? (
                <p className="empty" style={{ padding: '0.75rem' }}>Loading staffing rules…</p>
              ) : rulesQuery.isError ? (
                <div className="empty" style={{ padding: '0.75rem' }}>
                  <p style={{ color: 'var(--danger)', margin: '0 0 0.4rem' }}>Could not load staffing rules.</p>
                  <button type="button" className="secondary small" onClick={() => void rulesQuery.refetch()}>
                    Retry
                  </button>
                </div>
              ) : (rulesQuery.data ?? []).length === 0 ? (
                <p className="muted" style={{ fontStyle: 'italic', margin: '0.5rem 0' }}>
                  No minimum staffing rules configured for this ward.
                </p>
              ) : (
                <div style={{ overflowX: 'auto', marginTop: '0.4rem' }}>
                  <table style={{ margin: 0, fontSize: '0.85rem' }}>
                    <thead>
                      <tr>
                        <th>Role</th>
                        <th>Required qualification</th>
                        <th>Minimum headcount</th>
                        <th>Current on duty</th>
                        <th>Policy status</th>
                      </tr>
                    </thead>
                    <tbody>
                      {(rulesQuery.data ?? []).map((rule) => {
                        const onDutyForRole = ward.by_role?.[rule.required_role] ?? 0;
                        const isMet = onDutyForRole >= rule.minimum_headcount;
                        const diff = onDutyForRole - rule.minimum_headcount;

                        return (
                          <tr key={rule.id}>
                            <td>
                              <strong>{staffRoleLabels[rule.required_role] ?? rule.required_role}</strong>
                            </td>
                            <td>
                              {rule.required_skill_name ? (
                                <span className="badge" style={{ fontSize: '0.75rem' }}>
                                  {rule.required_skill_name}
                                </span>
                              ) : (
                                <span className="muted">None (any)</span>
                              )}
                            </td>
                            <td>
                              <strong>{rule.minimum_headcount}</strong>
                            </td>
                            <td>
                              {onDutyForRole}
                            </td>
                            <td>
                              {isMet ? (
                                <span className="badge" style={{ fontSize: '0.75rem' }}>
                                  Fulfilled ({diff > 0 ? `+${diff}` : 'met'})
                                </span>
                              ) : (
                                <span className="badge severity-high" style={{ fontSize: '0.75rem' }}>
                                  Shortfall ({diff})
                                </span>
                              )}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </td>
        </tr>
      )}
    </>
  );
}

// -------------------------------------------------------------
// Configure Ward Staffing Rules Dialog
// -------------------------------------------------------------

function ConfigureWardStaffingRulesDialog({
  ward,
  isOpen,
  onClose,
}: {
  ward: WardCoverageDto | null;
  isOpen: boolean;
  onClose: () => void;
}) {
  if (!ward) return null;

  return (
    <ActionDialog
      title={`Staffing rules — ${ward.ward_name}`}
      isOpen={isOpen}
      onClose={onClose}
      size="wide"
    >
      {isOpen && (
        <ConfigureWardStaffingRulesForm
          ward={ward}
          onClose={onClose}
        />
      )}
    </ActionDialog>
  );
}

function ConfigureWardStaffingRulesForm({
  ward,
  onClose,
}: {
  ward: WardCoverageDto;
  onClose: () => void;
}) {
  const rulesQuery = useQuery(getWardStaffingRulesOptions({ path: { wardId: ward.ward_id } }));
  const skillsQuery = useQuery(listSkillsOptions());

  if (rulesQuery.isLoading || skillsQuery.isLoading) {
    return <p className="empty">Loading current rules and qualifications…</p>;
  }

  if (rulesQuery.isError) {
    return (
      <div className="empty">
        <p style={{ color: 'var(--danger)' }}>Could not load existing staffing rules.</p>
        <button type="button" className="secondary small" onClick={() => void rulesQuery.refetch()}>
          Retry
        </button>
      </div>
    );
  }

  return (
    <ConfigureWardStaffingRulesEditor
      ward={ward}
      initialRules={rulesQuery.data ?? []}
      skills={skillsQuery.data ?? []}
      onClose={onClose}
    />
  );
}

type RuleRow = {
  keyId: string;
  required_role: StaffRole;
  required_skill_id: string;
  minimum_headcount: number;
};

function ConfigureWardStaffingRulesEditor({
  ward,
  initialRules,
  skills,
  onClose,
}: {
  ward: WardCoverageDto;
  initialRules: WardStaffingRuleDto[];
  skills: SkillDto[];
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [rules, setRules] = useState<RuleRow[]>(() =>
    initialRules.map((r, i) => ({
      keyId: r.id || `rule-${i}-${Date.now()}`,
      required_role: r.required_role,
      required_skill_id: r.required_skill_id ?? '',
      minimum_headcount: r.minimum_headcount,
    }))
  );

  const [formError, setFormError] = useState<string | null>(null);

  const setRulesMutation = useMutation({
    ...setWardStaffingRulesMutation(),
  });

  function handleAddRow() {
    setRules((prev) => [
      ...prev,
      {
        keyId: `rule-new-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`,
        required_role: 'ward_nurse',
        required_skill_id: '',
        minimum_headcount: 1,
      },
    ]);
  }

  function handleRemoveRow(index: number) {
    setRules((prev) => prev.filter((_, i) => i !== index));
  }

  function handleUpdateRow(index: number, patch: Partial<RuleRow>) {
    setRules((prev) =>
      prev.map((row, i) => (i === index ? { ...row, ...patch } : row))
    );
  }

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setFormError(null);

    // Validation
    const seen = new Set<string>();
    for (const rule of rules) {
      if (rule.minimum_headcount < 1) {
        setFormError('Minimum headcount must be at least 1 for all rules.');
        return;
      }
      const dedupeKey = `${rule.required_role}:${rule.required_skill_id || 'all'}`;
      if (seen.has(dedupeKey)) {
        const roleLabel = staffRoleLabels[rule.required_role] ?? rule.required_role;
        setFormError(
          `Duplicate rule: each combination of role (${roleLabel}) and required qualification must be unique.`
        );
        return;
      }
      seen.add(dedupeKey);
    }

    const body: WardStaffingRuleInput[] = rules.map((r) => ({
      required_role: r.required_role,
      required_skill_id: r.required_skill_id.trim() ? r.required_skill_id.trim() : null,
      minimum_headcount: r.minimum_headcount,
    }));

    setRulesMutation.mutate(
      {
        path: { wardId: ward.ward_id },
        body,
      },
      {
        onSuccess: (data) => {
          toast.success(`Staffing rules updated for ${ward.ward_name}.`);
          if (data.shifts_now_disagreeing && data.shifts_now_disagreeing.length > 0) {
            toast.warning(
              `${data.shifts_now_disagreeing.length} existing shift(s) now have headcount disagreeing with the updated rules.`
            );
          }
          // Invalidate staffing rules for this ward
          void queryClient.invalidateQueries({
            queryKey: getWardStaffingRulesQueryKey({ path: { wardId: ward.ward_id } }),
          });
          // Invalidate ward coverage overview snapshot
          void queryClient.invalidateQueries({
            predicate: (q) => {
              const k = q.queryKey[0];
              return typeof k === 'object' && k !== null && (k as any)._id === 'getWardCoverage';
            },
          });
          onClose();
        },
        onError: (err: any) => {
          const detail =
            err?.detail || err?.message || 'Failed to update ward staffing rules.';
          setFormError(detail);
          toast.error(detail);
        },
      }
    );
  }

  return (
    <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
      <p className="muted" style={{ margin: 0, fontSize: '0.85rem' }}>
        Configure the minimum staffing requirements for <strong>{ward.ward_name}</strong>.
        Rules establish the baseline headcount per role and mandatory qualifications needed for shifts.
      </p>

      {formError && (
        <div
          className="card"
          style={{
            background: 'var(--danger-bg, #fee)',
            borderColor: 'var(--danger)',
            padding: '0.6rem 0.8rem',
          }}
        >
          <p style={{ color: 'var(--danger)', margin: 0, fontSize: '0.85rem' }}>{formError}</p>
        </div>
      )}

      {rules.length === 0 ? (
        <div className="empty" style={{ padding: '1.5rem', textAlign: 'center' }}>
          <p style={{ margin: '0 0 0.5rem' }}>
            No minimum staffing rules are currently configured for this ward.
          </p>
          <button type="button" className="secondary small" onClick={handleAddRow}>
            + Add first rule
          </button>
        </div>
      ) : (
        <div style={{ overflowX: 'auto' }}>
          <table style={{ margin: 0, width: '100%' }}>
            <thead>
              <tr>
                <th style={{ width: '35%' }}>Required role</th>
                <th style={{ width: '40%' }}>Required qualification (optional)</th>
                <th style={{ width: '15%' }}>Min headcount</th>
                <th style={{ width: '10%', textAlign: 'right' }}>Action</th>
              </tr>
            </thead>
            <tbody>
              {rules.map((rule, idx) => (
                <tr key={rule.keyId}>
                  <td>
                    <select
                      value={rule.required_role}
                      onChange={(e) =>
                        handleUpdateRow(idx, { required_role: e.target.value as StaffRole })
                      }
                      disabled={setRulesMutation.isPending}
                      style={{ width: '100%', fontSize: '0.85rem' }}
                    >
                      {staffRoles.map((r) => (
                        <option key={r} value={r}>
                          {staffRoleLabels[r] ?? r}
                        </option>
                      ))}
                    </select>
                  </td>
                  <td>
                    <select
                      value={rule.required_skill_id}
                      onChange={(e) => handleUpdateRow(idx, { required_skill_id: e.target.value })}
                      disabled={setRulesMutation.isPending}
                      style={{ width: '100%', fontSize: '0.85rem' }}
                    >
                      <option value="">None (any qualification)</option>
                      {skills.map((skill) => (
                        <option key={skill.id} value={skill.id}>
                          {skill.name}
                        </option>
                      ))}
                    </select>
                  </td>
                  <td>
                    <input
                      type="number"
                      min={1}
                      max={99}
                      value={rule.minimum_headcount}
                      onChange={(e) =>
                        handleUpdateRow(idx, {
                          minimum_headcount: Math.max(1, parseInt(e.target.value, 10) || 1),
                        })
                      }
                      disabled={setRulesMutation.isPending}
                      style={{ width: '100%', fontSize: '0.85rem' }}
                    />
                  </td>
                  <td style={{ textAlign: 'right' }}>
                    <button
                      type="button"
                      className="secondary small"
                      onClick={() => handleRemoveRow(idx)}
                      disabled={setRulesMutation.isPending}
                    >
                      Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginTop: '0.5rem',
          flexWrap: 'wrap',
          gap: '0.5rem',
        }}
      >
        <button
          type="button"
          className="secondary small"
          onClick={handleAddRow}
          disabled={setRulesMutation.isPending}
        >
          + Add rule
        </button>

        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <button
            type="button"
            className="secondary"
            onClick={onClose}
            disabled={setRulesMutation.isPending}
          >
            Cancel
          </button>
          <button type="submit" disabled={setRulesMutation.isPending}>
            {setRulesMutation.isPending ? 'Saving rules…' : 'Save staffing rules'}
          </button>
        </div>
      </div>
    </form>
  );
}
