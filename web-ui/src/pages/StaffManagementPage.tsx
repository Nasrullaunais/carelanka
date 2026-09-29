import { useState } from 'react';
import type { FormEvent, ReactNode } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  createSkillMutation,
  createStaffMemberMutation,
  deactivateStaffMemberMutation,
  getStaffMemberOptions,
  grantStaffSkillMutation,
  listSkillsOptions,
  listSkillsQueryKey,
  listStaffOptions,
  listStaffSkillsOptions,
  listStaffSkillsQueryKey,
  lookupStaffMutation,
  reactivateStaffMemberMutation,
  retireSkillMutation,
  revokeStaffSkillMutation,
  updateSkillMutation,
  updateStaffMemberMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type {
  AllocationSummaryDto,
  CreateSkillRequest,
  CreateStaffMemberRequest,
  GrantStaffSkillRequest,
  SkillDto,
  StaffLookupResult,
  StaffMemberDetailDto,
  StaffRole,
  StaffSkillDto,
  StaffSummaryDto,
  UpdateSkillRequest,
  UpdateStaffMemberRequest,
} from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { localDateTime } from '../types/datetime';
import { canManageStaff, canViewStaff } from '../types/permissions';
import { staffRoleHints, staffRoleLabels, staffRoles } from '../types/staff';

const PAGE_SIZE = 15;

export function StaffManagementPage() {
  const session = useSession();
  const queryClient = useQueryClient();
  const role = session?.principal.role;

  const canView = canViewStaff(role);
  const canManage = canManageStaff(role);

  // Filters state
  const [search, setSearch] = useState('');
  const [submittedSearch, setSubmittedSearch] = useState('');
  const [selectedRole, setSelectedRole] = useState<StaffRole | ''>('');
  const [selectedDepartment, setSelectedDepartment] = useState('');
  const [includeInactive, setIncludeInactive] = useState(false);
  const [page, setPage] = useState(1);

  // Tab state
  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = searchParams.get('tab') === 'skills' ? 'skills' : 'directory';
  const setActiveTab = (tab: 'directory' | 'skills') => {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      if (tab === 'skills') {
        next.set('tab', 'skills');
      } else {
        next.delete('tab');
      }
      return next;
    });
  };

  // Modals state
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [detailStaffId, setDetailStaffId] = useState<string | null>(null);
  const [editStaffMember, setEditStaffMember] = useState<StaffMemberDetailDto | StaffSummaryDto | null>(null);
  const [deactivateStaff, setDeactivateStaff] = useState<{ id: string; name: string } | null>(null);
  const [reactivateStaff, setReactivateStaff] = useState<{ id: string; name: string } | null>(null);
  const [showLookupSection, setShowLookupSection] = useState(false);

  // Skills Administration Modals state
  const [createSkillOpen, setCreateSkillOpen] = useState(false);
  const [editingSkill, setEditingSkill] = useState<SkillDto | null>(null);
  const [retiringSkill, setRetiringSkill] = useState<SkillDto | null>(null);
  const [grantingStaff, setGrantingStaff] = useState<{ id: string; name: string } | null>(null);
  const [revokingSkill, setRevokingSkill] = useState<{ staffId: string; staffName: string; skillId: string; skillName: string } | null>(null);

  // Queries
  const staffQuery = useQuery({
    ...listStaffOptions({
      query: {
        ...(submittedSearch ? { search: submittedSearch } : {}),
        ...(selectedRole ? { role: selectedRole } : {}),
        ...(selectedDepartment ? { department: selectedDepartment } : {}),
        includeInactive,
        page,
        pageSize: PAGE_SIZE,
      },
    }),
    enabled: canView,
  });

  const invalidateStaffQueries = () => {
    void queryClient.invalidateQueries({
      predicate: (query) => {
        const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;
        return id === 'listStaff' || id === 'listSkills' || id === 'listStaffSkills';
      },
    });
    if (detailStaffId) {
      void queryClient.invalidateQueries({
        predicate: (query) =>
          (query.queryKey[0] as { _id?: string } | undefined)?._id === 'getStaffMember',
      });
    }
  };

  if (!canView) {
    return (
      <>
        <h1>Staff management</h1>
        <p className="empty">
          Your role cannot view staff management. Hospital administrators and duty managers have access.
        </p>
      </>
    );
  }

  function handleSearchSubmit(event: FormEvent) {
    event.preventDefault();
    setSubmittedSearch(search.trim());
    setPage(1);
  }

  function handleResetFilters() {
    setSearch('');
    setSubmittedSearch('');
    setSelectedRole('');
    setSelectedDepartment('');
    setIncludeInactive(false);
    setPage(1);
  }

  const paged = staffQuery.data;
  const staffList = paged?.items ?? [];
  const totalPages = paged?.total_pages ?? 1;

  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '1rem', marginBottom: '0.5rem' }}>
        <div>
          <h1>Staff management</h1>
          <p className="muted">
            Staff directory, credentialed roles, account provisioning, and active roster status.
            {!canManage && ' (Duty Manager: view-only mode)'}
          </p>
        </div>

        <div className="actions">
          <Link
            to="/staff/shifts"
            className="secondary button"
            style={{ textDecoration: 'none' }}
          >
            Shifts & roster
          </Link>
          <Link
            to="/staff/coverage"
            className="secondary button"
            style={{ textDecoration: 'none' }}
          >
            Ward coverage
          </Link>
          <Link
            to="/staff/leave-approval"
            className="secondary button"
            style={{ textDecoration: 'none' }}
          >
            Leave requests
          </Link>
          <Link
            to="/staff/roster-proposals"
            className="secondary button"
            style={{ textDecoration: 'none' }}
          >
            Roster proposals
          </Link>
          <Link
            to="/staff/reports"
            className="secondary button"
            style={{ textDecoration: 'none' }}
          >
            Staff reports
          </Link>
          <button
            type="button"
            className="secondary"
            onClick={() => setShowLookupSection((prev) => !prev)}
          >
            {showLookupSection ? 'Hide quick lookup' : 'Quick ID lookup'}
          </button>
          <button
            type="button"
            className="secondary"
            onClick={() => setActiveTab(activeTab === 'directory' ? 'skills' : 'directory')}
          >
            {activeTab === 'directory' ? 'Skills administration' : 'Staff directory'}
          </button>
          {canManage && activeTab === 'directory' && (
            <button type="button" onClick={() => setShowCreateModal(true)}>
              + Add staff member
            </button>
          )}
          {canManage && activeTab === 'skills' && (
            <button type="button" onClick={() => setCreateSkillOpen(true)}>
              + Create skill
            </button>
          )}
        </div>
      </div>

      <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1.25rem', borderBottom: '1px solid var(--line)', paddingBottom: '0.5rem' }}>
        <button
          type="button"
          className={activeTab === 'directory' ? '' : 'secondary'}
          onClick={() => setActiveTab('directory')}
          style={{ fontWeight: activeTab === 'directory' ? 600 : 400 }}
        >
          Staff directory
        </button>
        <button
          type="button"
          className={activeTab === 'skills' ? '' : 'secondary'}
          onClick={() => setActiveTab('skills')}
          style={{ fontWeight: activeTab === 'skills' ? 600 : 400 }}
        >
          Skills administration
        </button>
      </div>

      {activeTab === 'skills' ? (
        <SkillsAdministrationView
          canManage={canManage}
          onCreateSkill={() => setCreateSkillOpen(true)}
          onEditSkill={(skill) => setEditingSkill(skill)}
          onRetireSkill={(skill) => setRetiringSkill(skill)}
          onGrantSkill={(id, name) => setGrantingStaff({ id, name })}
          onRevokeSkill={(staffId, staffName, skillId, skillName) =>
            setRevokingSkill({ staffId, staffName, skillId, skillName })
          }
        />
      ) : (
        <>
          {showLookupSection && (
            <StaffLookupCard
              onOpenDetails={(id) => {
                setDetailStaffId(id);
              }}
            />
          )}

          {/* Filter bar */}
          <div className="card">
        <h2>Filter directory</h2>
        <form onSubmit={handleSearchSubmit}>
          <div className="row">
            <div>
              <label htmlFor="staff-search">Search keyword</label>
              <input
                id="staff-search"
                type="text"
                placeholder="Search by name, employee #, or email..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
            <div>
              <label htmlFor="staff-role-filter">Staff role</label>
              <select
                id="staff-role-filter"
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
            <div>
              <label htmlFor="staff-dept-filter">Department</label>
              <input
                id="staff-dept-filter"
                type="text"
                placeholder="e.g. Cardiology, ER, ICU..."
                value={selectedDepartment}
                onChange={(e) => {
                  setSelectedDepartment(e.target.value);
                  setPage(1);
                }}
              />
            </div>
            <div>
              <label htmlFor="staff-status-filter">Status</label>
              <select
                id="staff-status-filter"
                value={includeInactive ? 'all' : 'active'}
                onChange={(e) => {
                  setIncludeInactive(e.target.value === 'all');
                  setPage(1);
                }}
              >
                <option value="active">Active staff only</option>
                <option value="all">All staff (including inactive)</option>
              </select>
            </div>
          </div>

          <div className="actions" style={{ marginTop: '0.85rem' }}>
            <button type="submit">Apply search</button>
            {(submittedSearch || selectedRole || selectedDepartment || includeInactive) && (
              <button type="button" className="secondary" onClick={handleResetFilters}>
                Reset filters
              </button>
            )}
          </div>
        </form>
      </div>

      {/* Staff directory table */}
      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.75rem' }}>
          <h2>Staff directory {paged ? `(${paged.total_items})` : ''}</h2>
        </div>

        {staffQuery.isLoading ? (
          <p className="empty">Loading staff directory...</p>
        ) : staffQuery.isError ? (
          <div className="empty">
            <p style={{ color: 'var(--danger)' }}>Failed to load staff list.</p>
            <button type="button" className="secondary" onClick={() => void staffQuery.refetch()}>
              Retry
            </button>
          </div>
        ) : staffList.length === 0 ? (
          <div className="empty">
            <p>No staff members found matching the selected filters.</p>
            {(submittedSearch || selectedRole || selectedDepartment || includeInactive) && (
              <button type="button" className="secondary" onClick={handleResetFilters}>
                Clear filters
              </button>
            )}
          </div>
        ) : (
          <>
            <table>
              <thead>
                <tr>
                  <th>Staff member</th>
                  <th>Role</th>
                  <th>Department</th>
                  <th>Skills</th>
                  <th>Status</th>
                  <th style={{ textAlign: 'right' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {staffList.map((member) => (
                  <tr key={member.id}>
                    <td>
                      <strong>
                        <button
                          type="button"
                          className="linklike"
                          onClick={() => setDetailStaffId(member.id)}
                        >
                          {member.full_name}
                        </button>
                      </strong>
                      <br />
                      <span className="muted" style={{ fontSize: '0.75rem' }}>
                        ID: {member.id.slice(0, 8)}...
                      </span>
                    </td>
                    <td>
                      <span className="badge">
                        {staffRoleLabels[member.role] ?? member.role}
                      </span>
                    </td>
                    <td>{member.department || <span className="muted">—</span>}</td>
                    <td>
                      {member.skill_count > 0 ? (
                        <span>
                          {member.skill_count} skill{member.skill_count === 1 ? '' : 's'}
                        </span>
                      ) : (
                        <span className="muted">None</span>
                      )}
                    </td>
                    <td>
                      {member.is_active ? (
                        <span className="badge">Active</span>
                      ) : (
                        <span className="badge retired">Inactive</span>
                      )}
                    </td>
                    <td>
                      <div className="actions" style={{ justifyContent: 'flex-end' }}>
                        <button
                          type="button"
                          className="secondary small"
                          onClick={() => setDetailStaffId(member.id)}
                        >
                          Details
                        </button>
                        {canManage && (
                          <>
                            <button
                              type="button"
                              className="secondary small"
                              onClick={() => setEditStaffMember(member)}
                            >
                              Edit
                            </button>
                            {member.is_active ? (
                              <button
                                type="button"
                                className="secondary danger small"
                                onClick={() =>
                                  setDeactivateStaff({ id: member.id, name: member.full_name })
                                }
                              >
                                Deactivate
                              </button>
                            ) : (
                              <button
                                type="button"
                                className="secondary small"
                                onClick={() =>
                                  setReactivateStaff({ id: member.id, name: member.full_name })
                                }
                              >
                                Reactivate
                              </button>
                            )}
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
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
                  Page {paged?.page ?? page} of {totalPages} · {paged?.total_items ?? 0} staff members
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
      </>
      )}

      {/* Modal: Create Staff Member */}
      {showCreateModal && (
        <CreateStaffModal
          onClose={() => setShowCreateModal(false)}
          onSuccess={() => {
            setShowCreateModal(false);
            invalidateStaffQueries();
          }}
        />
      )}

      {/* Modal: Staff Details */}
      {detailStaffId && (
        <StaffDetailModal
          staffId={detailStaffId}
          canManage={canManage}
          onClose={() => setDetailStaffId(null)}
          onEdit={(detail) => {
            setDetailStaffId(null);
            setEditStaffMember(detail);
          }}
          onDeactivate={(id, name) => {
            setDetailStaffId(null);
            setDeactivateStaff({ id, name });
          }}
          onReactivate={(id, name) => {
            setDetailStaffId(null);
            setReactivateStaff({ id, name });
          }}
          onGrantSkill={(id, name) => setGrantingStaff({ id, name })}
          onRevokeSkill={(staffId, staffName, skillId, skillName) =>
            setRevokingSkill({ staffId, staffName, skillId, skillName })
          }
        />
      )}

      {/* Modal: Edit Staff Member */}
      {editStaffMember && (
        <EditStaffModal
          staff={editStaffMember}
          onClose={() => setEditStaffMember(null)}
          onSuccess={() => {
            setEditStaffMember(null);
            invalidateStaffQueries();
          }}
        />
      )}

      {/* Modal: Deactivate Staff Member Confirmation */}
      {deactivateStaff && (
        <DeactivateStaffModal
          staffId={deactivateStaff.id}
          staffName={deactivateStaff.name}
          onClose={() => setDeactivateStaff(null)}
          onSuccess={() => {
            setDeactivateStaff(null);
            invalidateStaffQueries();
          }}
        />
      )}

      {/* Modal: Reactivate Staff Member Confirmation */}
      {reactivateStaff && (
        <ReactivateStaffModal
          staffId={reactivateStaff.id}
          staffName={reactivateStaff.name}
          onClose={() => setReactivateStaff(null)}
          onSuccess={() => {
            setReactivateStaff(null);
            invalidateStaffQueries();
          }}
        />
      )}

      {/* Modal: Create Skill */}
      {createSkillOpen && (
        <CreateSkillModal
          onClose={() => setCreateSkillOpen(false)}
          onSuccess={() => {
            void queryClient.invalidateQueries({ queryKey: listSkillsQueryKey() });
          }}
        />
      )}

      {/* Modal: Edit Skill */}
      {editingSkill && (
        <EditSkillModal
          skill={editingSkill}
          onClose={() => setEditingSkill(null)}
          onSuccess={() => {
            void queryClient.invalidateQueries({ queryKey: listSkillsQueryKey() });
            invalidateStaffQueries();
          }}
        />
      )}

      {/* Modal: Retire Skill Confirmation */}
      {retiringSkill && (
        <RetireSkillConfirmModal
          skill={retiringSkill}
          onClose={() => setRetiringSkill(null)}
          onSuccess={() => {
            void queryClient.invalidateQueries({ queryKey: listSkillsQueryKey() });
            invalidateStaffQueries();
          }}
        />
      )}

      {/* Modal: Grant Staff Skill */}
      {grantingStaff && (
        <GrantStaffSkillModal
          staffId={grantingStaff.id}
          staffName={grantingStaff.name}
          onClose={() => setGrantingStaff(null)}
          onSuccess={() => {
            invalidateStaffQueries();
            if (grantingStaff.id) {
              void queryClient.invalidateQueries({
                queryKey: listStaffSkillsQueryKey({ path: { id: grantingStaff.id } }),
              });
            }
          }}
        />
      )}

      {/* Modal: Revoke Staff Skill Confirmation */}
      {revokingSkill && (
        <RevokeStaffSkillConfirmModal
          revoking={revokingSkill}
          onClose={() => setRevokingSkill(null)}
          onSuccess={() => {
            invalidateStaffQueries();
            if (revokingSkill.staffId) {
              void queryClient.invalidateQueries({
                queryKey: listStaffSkillsQueryKey({ path: { id: revokingSkill.staffId } }),
              });
            }
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
// Quick Staff Lookup Component
// -------------------------------------------------------------

function StaffLookupCard({ onOpenDetails }: { onOpenDetails: (id: string) => void }) {
  const [lookupId, setLookupId] = useState('');
  const [lookupResult, setLookupResult] = useState<StaffLookupResult | null>(null);
  const [searched, setSearched] = useState(false);

  const lookupMutationState = useMutation({
    ...lookupStaffMutation(),
    onSuccess: (data) => {
      setSearched(true);
      if (data && data.length > 0) {
        setLookupResult(data[0]);
      } else {
        setLookupResult(null);
      }
    },
    onError: (err) => {
      toast.error('Lookup failed: ' + (err instanceof Error ? err.message : 'Invalid request'));
    },
  });

  function handleLookup(event: FormEvent) {
    event.preventDefault();
    const trimmed = lookupId.trim();
    if (!trimmed) {
      toast.error('Please enter a staff UUID to look up.');
      return;
    }

    lookupMutationState.mutate({
      body: {
        staff_ids: [trimmed],
      },
    });
  }

  return (
    <div className="card" style={{ borderColor: 'var(--accent)' }}>
      <h3>Quick Staff ID Verification</h3>
      <p className="muted" style={{ marginBottom: '0.85rem' }}>
        Directly verify a staff member's active status and identity using their unique system GUID.
      </p>

      <form onSubmit={handleLookup} style={{ display: 'flex', gap: '0.6rem', flexWrap: 'wrap', alignItems: 'flex-end' }}>
        <div style={{ flex: '1 1 18rem' }}>
          <label htmlFor="quick-staff-id">Staff UUID</label>
          <input
            id="quick-staff-id"
            type="text"
            placeholder="e.g. 3fa85f64-5717-4562-b3fc-2c963f66afa6"
            value={lookupId}
            onChange={(e) => {
              setLookupId(e.target.value);
              setSearched(false);
            }}
          />
        </div>
        <button type="submit" disabled={lookupMutationState.isPending}>
          {lookupMutationState.isPending ? 'Checking...' : 'Verify ID'}
        </button>
      </form>

      {searched && (
        <div style={{ marginTop: '1rem', padding: '0.75rem', background: 'var(--canvas)', borderRadius: '8px' }}>
          {lookupResult?.found ? (
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.6rem' }}>
              <div>
                <strong>{lookupResult.full_name}</strong> ·{' '}
                <span className="badge">
                  {lookupResult.role ? staffRoleLabels[lookupResult.role] ?? lookupResult.role : 'Staff'}
                </span>{' '}
                {lookupResult.is_active ? (
                  <span className="badge">Active</span>
                ) : (
                  <span className="badge retired">Inactive</span>
                )}
              </div>
              <button
                type="button"
                className="secondary small"
                onClick={() => onOpenDetails(lookupResult.staff_id)}
              >
                Open full profile
              </button>
            </div>
          ) : (
            <p className="muted" style={{ color: 'var(--danger)', margin: 0 }}>
              No staff member found matching ID: {lookupId}
            </p>
          )}
        </div>
      )}
    </div>
  );
}

// -------------------------------------------------------------
// Create Staff Member Modal
// -------------------------------------------------------------

function CreateStaffModal({
  onClose,
  onSuccess,
}: {
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('CareLanka#2026');
  const [phone, setPhone] = useState('');
  const [role, setRole] = useState<StaffRole>('ward_nurse');
  const [department, setDepartment] = useState('');
  const [selectedSkills, setSelectedSkills] = useState<string[]>([]);

  // Load available skills
  const skillsQuery = useQuery(listSkillsOptions());

  const createMutation = useMutation({
    ...createStaffMemberMutation(),
    onSuccess: (data) => {
      toast.success(`Staff member ${data.full_name} (${data.employee_number}) created.`);
      onSuccess();
    },
    onError: (err) => {
      const msg = (err as { detail?: string; title?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Failed to create staff member. Please check form values.';
      toast.error(msg);
    },
  });

  function handleSkillToggle(skillId: string) {
    setSelectedSkills((prev) =>
      prev.includes(skillId) ? prev.filter((id) => id !== skillId) : [...prev, skillId],
    );
  }

  function handleGeneratePassword() {
    const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%';
    let generated = 'CL-';
    for (let i = 0; i < 10; i++) {
      generated += chars.charAt(Math.floor(Math.random() * chars.length));
    }
    setPassword(generated);
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();

    if (!firstName.trim() || !lastName.trim()) {
      toast.error('First name and last name are required.');
      return;
    }
    if (!email.trim()) {
      toast.error('A valid email address is required.');
      return;
    }
    if (password.length < 12) {
      toast.error('Temporary password must be at least 12 characters.');
      return;
    }

    const payload: CreateStaffMemberRequest = {
      first_name: firstName.trim(),
      last_name: lastName.trim(),
      email: email.trim(),
      temporary_password: password,
      role,
      phone_number: phone.trim() || null,
      department: department.trim() || null,
      skill_ids: selectedSkills.length > 0 ? selectedSkills : null,
    };

    createMutation.mutate({ body: payload });
  }

  const availableSkills = skillsQuery.data ?? [];

  return (
    <ModalDialog title="Add new staff member" onClose={onClose} maxWidth="36rem">
      <form onSubmit={handleSubmit}>
        <div className="row">
          <div>
            <label htmlFor="create-first-name">First name *</label>
            <input
              id="create-first-name"
              type="text"
              required
              value={firstName}
              onChange={(e) => setFirstName(e.target.value)}
            />
          </div>
          <div>
            <label htmlFor="create-last-name">Last name *</label>
            <input
              id="create-last-name"
              type="text"
              required
              value={lastName}
              onChange={(e) => setLastName(e.target.value)}
            />
          </div>
        </div>

        <div className="row" style={{ marginTop: '0.85rem' }}>
          <div>
            <label htmlFor="create-email">Work email *</label>
            <input
              id="create-email"
              type="email"
              required
              placeholder="e.g. staff.member@carelanka.lk"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </div>
          <div>
            <label htmlFor="create-phone">Phone number</label>
            <input
              id="create-phone"
              type="tel"
              placeholder="+94 77 123 4567"
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
            />
          </div>
        </div>

        <div className="row" style={{ marginTop: '0.85rem' }}>
          <div>
            <label htmlFor="create-role">Assigned role *</label>
            <select
              id="create-role"
              value={role}
              onChange={(e) => setRole(e.target.value as StaffRole)}
            >
              {staffRoles.map((r) => (
                <option key={r} value={r}>
                  {staffRoleLabels[r]}
                </option>
              ))}
            </select>
            <span className="hint" style={{ marginTop: '0.2rem', display: 'block' }}>
              {staffRoleHints[role]}
            </span>
          </div>
          <div>
            <label htmlFor="create-department">Department / Unit</label>
            <input
              id="create-department"
              type="text"
              placeholder="e.g. Emergency Care, Intensive Care, Ward 3"
              value={department}
              onChange={(e) => setDepartment(e.target.value)}
            />
          </div>
        </div>

        <div style={{ marginTop: '0.85rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <label htmlFor="create-temp-password">Temporary password *</label>
            <button
              type="button"
              className="linklike small"
              onClick={handleGeneratePassword}
              style={{ fontSize: '0.75rem' }}
            >
              Generate random
            </button>
          </div>
          <input
            id="create-temp-password"
            type="text"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
          <span className="hint" style={{ marginTop: '0.2rem', display: 'block' }}>
            Must be at least 12 characters. The user will be required to change this upon first sign-in.
          </span>
        </div>

        {availableSkills.length > 0 && (
          <div style={{ marginTop: '1rem' }}>
            <label>Initial skills & certifications</label>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(13rem, 1fr))', gap: '0.4rem', marginTop: '0.35rem' }}>
              {availableSkills.map((skill) => (
                <label key={skill.id} style={{ fontWeight: 'normal', cursor: 'pointer', display: 'flex', alignItems: 'center' }}>
                  <input
                    type="checkbox"
                    checked={selectedSkills.includes(skill.id)}
                    onChange={() => handleSkillToggle(skill.id)}
                  />
                  <span>{skill.name}</span>
                </label>
              ))}
            </div>
          </div>
        )}

        <div className="actions" style={{ marginTop: '1.25rem', justifyContent: 'flex-end' }}>
          <button type="button" className="secondary" onClick={onClose} disabled={createMutation.isPending}>
            Cancel
          </button>
          <button type="submit" disabled={createMutation.isPending}>
            {createMutation.isPending ? 'Provisioning...' : 'Provision account'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}

// -------------------------------------------------------------
// Staff Detail Modal
// -------------------------------------------------------------

function StaffDetailModal({
  staffId,
  canManage,
  onClose,
  onEdit,
  onDeactivate,
  onReactivate,
  onGrantSkill,
  onRevokeSkill,
}: {
  staffId: string;
  canManage: boolean;
  onClose: () => void;
  onEdit: (detail: StaffMemberDetailDto) => void;
  onDeactivate: (id: string, name: string) => void;
  onReactivate: (id: string, name: string) => void;
  onGrantSkill?: (id: string, name: string) => void;
  onRevokeSkill?: (staffId: string, staffName: string, skillId: string, skillName: string) => void;
}) {
  const detailQuery = useQuery(getStaffMemberOptions({ path: { id: staffId } }));
  const staffSkillsQuery = useQuery(listStaffSkillsOptions({ path: { id: staffId } }));
  const detail = detailQuery.data;
  const skillsList = staffSkillsQuery.data ?? detail?.skills ?? [];

  return (
    <ModalDialog
      title={detail ? `${detail.full_name}` : 'Staff profile details'}
      onClose={onClose}
      maxWidth="38rem"
    >
      {detailQuery.isLoading ? (
        <p className="empty">Loading staff member profile...</p>
      ) : detailQuery.isError || !detail ? (
        <div className="empty">
          <p style={{ color: 'var(--danger)' }}>Failed to load profile details.</p>
          <button type="button" className="secondary" onClick={() => void detailQuery.refetch()}>
            Retry
          </button>
        </div>
      ) : (
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem', borderBottom: '1px solid var(--line)', paddingBottom: '0.75rem' }}>
            <div>
              <span className="badge" style={{ marginRight: '0.4rem' }}>
                {staffRoleLabels[detail.role] ?? detail.role}
              </span>
              {detail.is_active ? (
                <span className="badge">Active</span>
              ) : (
                <span className="badge retired">Inactive</span>
              )}
            </div>
            <span className="muted" style={{ fontSize: '0.85rem' }}>
              Employee #: <strong>{detail.employee_number}</strong>
            </span>
          </div>

          <dl className="detail-grid">
            <div>
              <dt>Full name</dt>
              <dd><strong>{detail.full_name}</strong></dd>
            </div>
            <div>
              <dt>Employee ID</dt>
              <dd>{detail.employee_number}</dd>
            </div>
            <div>
              <dt>Work email</dt>
              <dd>
                <a href={`mailto:${detail.email}`} style={{ color: 'var(--accent)' }}>
                  {detail.email}
                </a>
              </dd>
            </div>
            <div>
              <dt>Phone number</dt>
              <dd>
                {detail.phone_number ? (
                  <a href={`tel:${detail.phone_number}`} style={{ color: 'var(--accent)' }}>
                    {detail.phone_number}
                  </a>
                ) : (
                  <span className="muted">—</span>
                )}
              </dd>
            </div>
            <div>
              <dt>Department</dt>
              <dd>{detail.department || <span className="muted">Not assigned</span>}</dd>
            </div>
            <div>
              <dt>Leave balance</dt>
              <dd>{detail.leave_balance_days !== undefined && detail.leave_balance_days !== null ? `${detail.leave_balance_days} days` : '0 days'}</dd>
            </div>
            <div>
              <dt>System UUID</dt>
              <dd style={{ fontSize: '0.75rem', fontFamily: 'monospace' }}>{detail.id}</dd>
            </div>
            <div>
              <dt>Member since</dt>
              <dd>{localDateTime(detail.created_at)}</dd>
            </div>
          </dl>

          {/* Skills section */}
          <div style={{ marginTop: '1.25rem', borderTop: '1px solid var(--line)', paddingTop: '0.85rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.4rem' }}>
              <h3 style={{ margin: 0 }}>Skills & Certifications ({skillsList.length})</h3>
              {canManage && (
                <button
                  type="button"
                  className="secondary small"
                  onClick={() => onGrantSkill?.(detail.id, detail.full_name)}
                >
                  + Grant skill
                </button>
              )}
            </div>
            {skillsList.length > 0 ? (
              <table style={{ marginTop: '0.4rem' }}>
                <thead>
                  <tr>
                    <th>Skill name</th>
                    <th>Status</th>
                    <th>Granted</th>
                    <th>Expires</th>
                    {canManage && <th style={{ textAlign: 'right' }}>Actions</th>}
                  </tr>
                </thead>
                <tbody>
                  {skillsList.map((s) => (
                    <tr key={s.skill_id}>
                      <td><strong>{s.skill_name}</strong></td>
                      <td>
                        {s.is_valid ? (
                          <span className="badge">Valid</span>
                        ) : (
                          <span className="badge retired">Expired</span>
                        )}
                      </td>
                      <td>{localDateTime(s.granted_at)}</td>
                      <td>
                        {s.expires_at ? localDateTime(s.expires_at) : <span className="muted">Permanent</span>}
                      </td>
                      {canManage && (
                        <td style={{ textAlign: 'right' }}>
                          <button
                            type="button"
                            className="secondary danger small"
                            onClick={() =>
                              onRevokeSkill?.(detail.id, detail.full_name, s.skill_id, s.skill_name)
                            }
                          >
                            Revoke
                          </button>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <p className="muted" style={{ fontStyle: 'italic', margin: '0.35rem 0' }}>
                No specialized skills currently attached.
              </p>
            )}
          </div>

          {/* Upcoming allocations section */}
          <div style={{ marginTop: '1.25rem', borderTop: '1px solid var(--line)', paddingTop: '0.85rem' }}>
            <h3>Upcoming Shift Allocations ({detail.upcoming_allocations?.length ?? 0})</h3>
            {detail.upcoming_allocations && detail.upcoming_allocations.length > 0 ? (
              <table style={{ marginTop: '0.4rem' }}>
                <thead>
                  <tr>
                    <th>Date</th>
                    <th>Time</th>
                    <th>Ward</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {detail.upcoming_allocations.map((a) => (
                    <tr key={a.allocation_id}>
                      <td><strong>{a.date}</strong></td>
                      <td>{a.start_time} - {a.end_time}</td>
                      <td>{a.ward_name}</td>
                      <td>
                        <span className="badge">{a.status}</span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <p className="muted" style={{ fontStyle: 'italic', margin: '0.35rem 0' }}>
                No future shift allocations scheduled.
              </p>
            )}
          </div>

          {/* Action buttons */}
          <div className="actions" style={{ marginTop: '1.5rem', justifyContent: 'flex-end', borderTop: '1px solid var(--line)', paddingTop: '0.85rem' }}>
            <button type="button" className="secondary" onClick={onClose}>
              Close
            </button>
            {canManage && (
              <>
                <button type="button" className="secondary" onClick={() => onEdit(detail)}>
                  Edit details
                </button>
                {detail.is_active ? (
                  <button
                    type="button"
                    className="danger"
                    onClick={() => onDeactivate(detail.id, detail.full_name)}
                  >
                    Deactivate member
                  </button>
                ) : (
                  <button
                    type="button"
                    onClick={() => onReactivate(detail.id, detail.full_name)}
                  >
                    Reactivate member
                  </button>
                )}
              </>
            )}
          </div>
        </div>
      )}
    </ModalDialog>
  );
}

// -------------------------------------------------------------
// Edit Staff Member Modal
// -------------------------------------------------------------

function EditStaffModal({
  staff,
  onClose,
  onSuccess,
}: {
  staff: StaffMemberDetailDto | StaffSummaryDto;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const detail = 'first_name' in staff ? (staff as StaffMemberDetailDto) : null;

  const [firstName, setFirstName] = useState(detail?.first_name ?? '');
  const [lastName, setLastName] = useState(detail?.last_name ?? '');
  const [phone, setPhone] = useState(detail?.phone_number ?? '');
  const [role, setRole] = useState<StaffRole>(staff.role);
  const [department, setDepartment] = useState(staff.department ?? '');

  const updateMutation = useMutation({
    ...updateStaffMemberMutation(),
    onSuccess: (res) => {
      toast.success(`Staff member ${res.staff_member.full_name} updated.`);
      if (res.affected_allocations && res.affected_allocations.length > 0) {
        toast.info(
          `Role change affected ${res.affected_allocations.length} shift allocation${
            res.affected_allocations.length === 1 ? '' : 's'
          }.`,
        );
      }
      onSuccess();
    },
    onError: (err) => {
      const msg = (err as { detail?: string; title?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Failed to update staff member.';
      toast.error(msg);
    },
  });

  function handleSubmit(event: FormEvent) {
    event.preventDefault();

    const payload: UpdateStaffMemberRequest = {
      ...(firstName.trim() ? { first_name: firstName.trim() } : {}),
      ...(lastName.trim() ? { last_name: lastName.trim() } : {}),
      phone_number: phone.trim() || null,
      role,
      department: department.trim() || null,
    };

    updateMutation.mutate({
      path: { id: staff.id },
      body: payload,
    });
  }

  return (
    <ModalDialog title={`Edit staff: ${staff.full_name}`} onClose={onClose} maxWidth="34rem">
      <form onSubmit={handleSubmit}>
        {'first_name' in staff && (
          <div className="row">
            <div>
              <label htmlFor="edit-first-name">First name</label>
              <input
                id="edit-first-name"
                type="text"
                value={firstName}
                onChange={(e) => setFirstName(e.target.value)}
              />
            </div>
            <div>
              <label htmlFor="edit-last-name">Last name</label>
              <input
                id="edit-last-name"
                type="text"
                value={lastName}
                onChange={(e) => setLastName(e.target.value)}
              />
            </div>
          </div>
        )}

        <div className="row" style={{ marginTop: '0.85rem' }}>
          <div>
            <label htmlFor="edit-role">Staff role</label>
            <select
              id="edit-role"
              value={role}
              onChange={(e) => setRole(e.target.value as StaffRole)}
            >
              {staffRoles.map((r) => (
                <option key={r} value={r}>
                  {staffRoleLabels[r]}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="edit-phone">Phone number</label>
            <input
              id="edit-phone"
              type="tel"
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
            />
          </div>
        </div>

        <div style={{ marginTop: '0.85rem' }}>
          <label htmlFor="edit-department">Department / Unit</label>
          <input
            id="edit-department"
            type="text"
            value={department}
            onChange={(e) => setDepartment(e.target.value)}
          />
        </div>

        <div className="hint" style={{ marginTop: '1rem' }}>
          Changing a staff member's role may automatically release or invalidate upcoming shift allocations where role requirements are violated.
        </div>

        <div className="actions" style={{ marginTop: '1.25rem', justifyContent: 'flex-end' }}>
          <button type="button" className="secondary" onClick={onClose} disabled={updateMutation.isPending}>
            Cancel
          </button>
          <button type="submit" disabled={updateMutation.isPending}>
            {updateMutation.isPending ? 'Saving...' : 'Save changes'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}

// -------------------------------------------------------------
// Deactivate Confirmation Modal
// -------------------------------------------------------------

function DeactivateStaffModal({
  staffId,
  staffName,
  onClose,
  onSuccess,
}: {
  staffId: string;
  staffName: string;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [reason, setReason] = useState('');
  const [effectiveDate, setEffectiveDate] = useState('');

  const deactivateMutation = useMutation({
    ...deactivateStaffMemberMutation(),
    onSuccess: () => {
      toast.success(`${staffName} has been deactivated.`);
      onSuccess();
    },
    onError: (err) => {
      const msg = (err as { detail?: string; title?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Failed to deactivate staff member.';
      toast.error(msg);
    },
  });

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!reason.trim()) {
      toast.error('Deactivation reason is required.');
      return;
    }

    deactivateMutation.mutate({
      path: { id: staffId },
      body: {
        reason: reason.trim(),
        ...(effectiveDate ? { effective_date: effectiveDate } : {}),
      },
    });
  }

  return (
    <ModalDialog title={`Deactivate: ${staffName}`} onClose={onClose} maxWidth="32rem">
      <form onSubmit={handleSubmit}>
        <div className="stub-note" style={{ borderColor: 'var(--danger)', color: 'var(--danger)', background: '#fdf2f2' }}>
          <strong>Warning:</strong> Deactivating a staff member immediately revokes their sign-in credentials and releases all scheduled upcoming shift allocations.
        </div>

        <div style={{ marginTop: '0.85rem' }}>
          <label htmlFor="deactivate-reason">Reason for deactivation *</label>
          <textarea
            id="deactivate-reason"
            rows={3}
            required
            placeholder="e.g. Resigned, contract end, extended leave, disciplinary..."
            value={reason}
            onChange={(e) => setReason(e.target.value)}
          />
        </div>

        <div style={{ marginTop: '0.85rem' }}>
          <label htmlFor="deactivate-date">Effective date (optional)</label>
          <input
            id="deactivate-date"
            type="date"
            value={effectiveDate}
            onChange={(e) => setEffectiveDate(e.target.value)}
          />
        </div>

        <div className="actions" style={{ marginTop: '1.25rem', justifyContent: 'flex-end' }}>
          <button type="button" className="secondary" onClick={onClose} disabled={deactivateMutation.isPending}>
            Cancel
          </button>
          <button
            type="submit"
            className="danger"
            disabled={deactivateMutation.isPending || !reason.trim()}
          >
            {deactivateMutation.isPending ? 'Deactivating...' : 'Confirm deactivation'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}

// -------------------------------------------------------------
// Reactivate Confirmation Modal
// -------------------------------------------------------------

function ReactivateStaffModal({
  staffId,
  staffName,
  onClose,
  onSuccess,
}: {
  staffId: string;
  staffName: string;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const reactivateMutation = useMutation({
    ...reactivateStaffMemberMutation(),
    onSuccess: (data) => {
      toast.success(`${data.full_name} reactivated successfully.`);
      onSuccess();
    },
    onError: (err) => {
      const msg = (err as { detail?: string; title?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Failed to reactivate staff member.';
      toast.error(msg);
    },
  });

  return (
    <ModalDialog title={`Reactivate: ${staffName}`} onClose={onClose} maxWidth="30rem">
      <p>
        Are you sure you want to reactivate <strong>{staffName}</strong>?
      </p>
      <p className="muted">
        Reactivation restores login permissions and allows the staff member to be assigned to ward shifts and rosters again.
      </p>

      <div className="actions" style={{ marginTop: '1.5rem', justifyContent: 'flex-end' }}>
        <button type="button" className="secondary" onClick={onClose} disabled={reactivateMutation.isPending}>
          Cancel
        </button>
        <button
          type="button"
          disabled={reactivateMutation.isPending}
          onClick={() => reactivateMutation.mutate({ path: { id: staffId } })}
        >
          {reactivateMutation.isPending ? 'Reactivating...' : 'Confirm reactivation'}
        </button>
      </div>
    </ModalDialog>
  );
}

// -------------------------------------------------------------
// Skills Administration View
// -------------------------------------------------------------

function SkillsAdministrationView({
  canManage,
  onCreateSkill,
  onEditSkill,
  onRetireSkill,
  onGrantSkill,
  onRevokeSkill,
}: {
  canManage: boolean;
  onCreateSkill: () => void;
  onEditSkill: (skill: SkillDto) => void;
  onRetireSkill: (skill: SkillDto) => void;
  onGrantSkill: (id: string, name: string) => void;
  onRevokeSkill: (staffId: string, staffName: string, skillId: string, skillName: string) => void;
}) {
  const skillsQuery = useQuery(listSkillsOptions());
  const [skillFilter, setSkillFilter] = useState('');

  // Staff member qualifications section state
  const [selectedStaffId, setSelectedStaffId] = useState<string>('');
  const staffQuery = useQuery(
    listStaffOptions({
      query: {
        pageSize: 100,
      },
    }),
  );

  const staffSkillsQuery = useQuery({
    ...listStaffSkillsOptions({ path: { id: selectedStaffId } }),
    enabled: Boolean(selectedStaffId),
  });

  const skills = (skillsQuery.data ?? []).filter((s) => {
    if (!skillFilter.trim()) return true;
    const q = skillFilter.toLowerCase();
    return (
      s.name.toLowerCase().includes(q) ||
      (s.description?.toLowerCase().includes(q) ?? false)
    );
  });

  const selectedStaff = staffQuery.data?.items.find((s) => s.id === selectedStaffId);
  const staffSkills: StaffSkillDto[] = staffSkillsQuery.data ?? [];

  return (
    <div>
      {/* Hospital Skills Catalog */}
      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.75rem', marginBottom: '1rem' }}>
          <div>
            <h2 style={{ margin: 0 }}>Hospital Skills Catalog</h2>
            <p className="muted" style={{ margin: '0.25rem 0 0 0' }}>
              Specialized clinical competencies, certifications, and qualification requirements.
            </p>
          </div>
          {canManage && (
            <button type="button" onClick={onCreateSkill}>
              + Create skill
            </button>
          )}
        </div>

        {/* Catalog search bar */}
        <div style={{ marginBottom: '1rem' }}>
          <label htmlFor="skill-filter">Search skills</label>
          <input
            id="skill-filter"
            type="text"
            placeholder="Search by skill name or description..."
            value={skillFilter}
            onChange={(e) => setSkillFilter(e.target.value)}
          />
        </div>

        {skillsQuery.isLoading ? (
          <p className="empty">Loading skills catalog...</p>
        ) : skillsQuery.isError ? (
          <div className="empty">
            <p style={{ color: 'var(--danger)' }}>Failed to load skills catalog.</p>
            <button type="button" className="secondary" onClick={() => void skillsQuery.refetch()}>
              Retry
            </button>
          </div>
        ) : skills.length === 0 ? (
          <p className="empty">No skills found matching your filters.</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Skill name</th>
                <th>Description</th>
                <th>Qualified staff</th>
                {canManage && <th style={{ textAlign: 'right' }}>Actions</th>}
              </tr>
            </thead>
            <tbody>
              {skills.map((s) => (
                <tr key={s.id}>
                  <td>
                    <strong>{s.name}</strong>
                  </td>
                  <td>{s.description || <span className="muted">—</span>}</td>
                  <td>
                    <span>
                      {s.staff_count} member{s.staff_count === 1 ? '' : 's'}
                    </span>
                  </td>
                  {canManage && (
                    <td>
                      <div className="actions" style={{ justifyContent: 'flex-end' }}>
                        <button
                          type="button"
                          className="secondary small"
                          onClick={() => onEditSkill(s)}
                        >
                          Edit
                        </button>
                        <button
                          type="button"
                          className="secondary danger small"
                          onClick={() => onRetireSkill(s)}
                        >
                          Retire
                        </button>
                      </div>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Staff Qualifications Management */}
      <div className="card" style={{ marginTop: '1.5rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.75rem', marginBottom: '1rem' }}>
          <div>
            <h2 style={{ margin: 0 }}>Staff Member Qualifications</h2>
            <p className="muted" style={{ margin: '0.25rem 0 0 0' }}>
              View, grant, or revoke skill certifications for individual healthcare personnel.
            </p>
          </div>
          {canManage && selectedStaff && (
            <button
              type="button"
              onClick={() => onGrantSkill(selectedStaff.id, selectedStaff.full_name)}
            >
              + Grant skill to member
            </button>
          )}
        </div>

        <div style={{ marginBottom: '1rem' }}>
          <label htmlFor="staff-member-select">Select staff member</label>
          <select
            id="staff-member-select"
            value={selectedStaffId}
            onChange={(e) => setSelectedStaffId(e.target.value)}
          >
            <option value="">-- Choose a staff member to view qualifications --</option>
            {(staffQuery.data?.items ?? []).map((m) => (
              <option key={m.id} value={m.id}>
                {m.full_name} ({staffRoleLabels[m.role] ?? m.role})
              </option>
            ))}
          </select>
        </div>

        {selectedStaff ? (
          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '0.75rem', background: 'var(--surface)', border: '1px solid var(--line)', borderRadius: '6px', marginBottom: '1rem' }}>
              <div>
                <strong>{selectedStaff.full_name}</strong>
                <span className="badge" style={{ marginLeft: '0.5rem' }}>
                  {staffRoleLabels[selectedStaff.role] ?? selectedStaff.role}
                </span>
                {selectedStaff.department && (
                  <span className="muted" style={{ marginLeft: '0.5rem' }}>
                    Dept: {selectedStaff.department}
                  </span>
                )}
              </div>
              <span className="muted" style={{ fontSize: '0.85rem' }}>
                ID: {selectedStaff.id.slice(0, 8)}...
              </span>
            </div>

            {staffSkillsQuery.isLoading ? (
              <p className="empty">Loading qualifications...</p>
            ) : staffSkillsQuery.isError ? (
              <div className="empty">
                <p style={{ color: 'var(--danger)' }}>Failed to load qualifications for this staff member.</p>
                <button type="button" className="secondary" onClick={() => void staffSkillsQuery.refetch()}>
                  Retry
                </button>
              </div>
            ) : staffSkills.length === 0 ? (
              <p className="empty">No specialized skills currently assigned to {selectedStaff.full_name}.</p>
            ) : (
              <table>
                <thead>
                  <tr>
                    <th>Skill name</th>
                    <th>Status</th>
                    <th>Valid from</th>
                    <th>Expires</th>
                    <th>Granted</th>
                    {canManage && <th style={{ textAlign: 'right' }}>Actions</th>}
                  </tr>
                </thead>
                <tbody>
                  {staffSkills.map((s) => (
                    <tr key={s.skill_id}>
                      <td><strong>{s.skill_name}</strong></td>
                      <td>
                        {s.is_valid ? (
                          <span className="badge">Valid</span>
                        ) : (
                          <span className="badge retired">Expired</span>
                        )}
                      </td>
                      <td>{s.valid_from ?? '—'}</td>
                      <td>
                        {s.expires_at ? localDateTime(s.expires_at) : <span className="muted">Permanent</span>}
                      </td>
                      <td>{localDateTime(s.granted_at)}</td>
                      {canManage && (
                        <td style={{ textAlign: 'right' }}>
                          <button
                            type="button"
                            className="secondary danger small"
                            onClick={() =>
                              onRevokeSkill(
                                selectedStaff.id,
                                selectedStaff.full_name,
                                s.skill_id,
                                s.skill_name,
                              )
                            }
                          >
                            Revoke
                          </button>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        ) : (
          <p className="empty">Select a staff member from the dropdown above to view and manage their skills.</p>
        )}
      </div>
    </div>
  );
}

// -------------------------------------------------------------
// Create Skill Modal
// -------------------------------------------------------------

function CreateSkillModal({
  onClose,
  onSuccess,
}: {
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');

  const createMutation = useMutation({
    ...createSkillMutation(),
    onSuccess: (data) => {
      toast.success(`Skill "${data.name}" created successfully.`);
      onSuccess();
      onClose();
    },
    onError: (err) => {
      const msg =
        (err as { detail?: string; title?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Failed to create skill.';
      toast.error(msg);
    },
  });

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (name.trim().length < 2) {
      toast.error('Skill name must be at least 2 characters.');
      return;
    }

    const payload: CreateSkillRequest = {
      name: name.trim(),
      ...(description.trim() ? { description: description.trim() } : {}),
    };

    createMutation.mutate({ body: payload });
  }

  return (
    <ModalDialog title="Create new skill qualification" onClose={onClose} maxWidth="32rem">
      <form onSubmit={handleSubmit}>
        <div>
          <label htmlFor="create-skill-name">Skill name *</label>
          <input
            id="create-skill-name"
            type="text"
            required
            minLength={2}
            maxLength={100}
            placeholder="e.g. Advanced Cardiac Life Support (ACLS)"
            value={name}
            onChange={(e) => setName(e.target.value)}
          />
        </div>

        <div style={{ marginTop: '0.85rem' }}>
          <label htmlFor="create-skill-desc">Description (optional)</label>
          <textarea
            id="create-skill-desc"
            rows={3}
            maxLength={500}
            placeholder="Detailed description or requirements of this qualification..."
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
        </div>

        <div className="actions" style={{ marginTop: '1.25rem', justifyContent: 'flex-end' }}>
          <button type="button" className="secondary" onClick={onClose} disabled={createMutation.isPending}>
            Cancel
          </button>
          <button type="submit" disabled={createMutation.isPending || name.trim().length < 2}>
            {createMutation.isPending ? 'Creating...' : 'Create skill'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}

// -------------------------------------------------------------
// Edit Skill Modal
// -------------------------------------------------------------

function EditSkillModal({
  skill,
  onClose,
  onSuccess,
}: {
  skill: SkillDto;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [name, setName] = useState(skill.name);
  const [description, setDescription] = useState(skill.description ?? '');

  const updateMutation = useMutation({
    ...updateSkillMutation(),
    onSuccess: (data) => {
      toast.success(`Skill "${data.name}" updated successfully.`);
      onSuccess();
      onClose();
    },
    onError: (err) => {
      const msg =
        (err as { detail?: string; title?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Failed to update skill.';
      toast.error(msg);
    },
  });

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (name.trim().length < 2) {
      toast.error('Skill name must be at least 2 characters.');
      return;
    }

    const payload: UpdateSkillRequest = {
      name: name.trim(),
      description: description.trim() || null,
    };

    updateMutation.mutate({
      path: { id: skill.id },
      body: payload,
    });
  }

  return (
    <ModalDialog title={`Edit skill: ${skill.name}`} onClose={onClose} maxWidth="32rem">
      <form onSubmit={handleSubmit}>
        <div>
          <label htmlFor="edit-skill-name">Skill name *</label>
          <input
            id="edit-skill-name"
            type="text"
            required
            minLength={2}
            maxLength={100}
            value={name}
            onChange={(e) => setName(e.target.value)}
          />
        </div>

        <div style={{ marginTop: '0.85rem' }}>
          <label htmlFor="edit-skill-desc">Description (optional)</label>
          <textarea
            id="edit-skill-desc"
            rows={3}
            maxLength={500}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
        </div>

        <div className="actions" style={{ marginTop: '1.25rem', justifyContent: 'flex-end' }}>
          <button type="button" className="secondary" onClick={onClose} disabled={updateMutation.isPending}>
            Cancel
          </button>
          <button type="submit" disabled={updateMutation.isPending || name.trim().length < 2}>
            {updateMutation.isPending ? 'Saving...' : 'Save changes'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}

// -------------------------------------------------------------
// Retire Skill Confirmation Modal
// -------------------------------------------------------------

function RetireSkillConfirmModal({
  skill,
  onClose,
  onSuccess,
}: {
  skill: SkillDto;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [conflictError, setConflictError] = useState<string | null>(null);

  const retireMutation = useMutation({
    ...retireSkillMutation(),
    onSuccess: () => {
      toast.success(`Skill "${skill.name}" retired successfully.`);
      onSuccess();
      onClose();
    },
    onError: (err) => {
      const msg =
        (err as { detail?: string; title?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Failed to retire skill. Ensure no active or upcoming shifts require this qualification.';
      setConflictError(msg);
      toast.error(msg);
    },
  });

  return (
    <ModalDialog title={`Retire skill: ${skill.name}`} onClose={onClose} maxWidth="32rem">
      <div className="stub-note" style={{ borderColor: 'var(--danger)', color: 'var(--danger)', background: '#fdf2f2' }}>
        <strong>Warning:</strong> Retiring this skill will prevent it from being granted to staff members or assigned to new ward staffing rules.
      </div>

      <p style={{ marginTop: '0.85rem' }}>
        Currently <strong>{skill.staff_count}</strong> staff member{skill.staff_count === 1 ? '' : 's'} hold this qualification.
      </p>

      {conflictError && (
        <div style={{ marginTop: '0.85rem', padding: '0.75rem', background: '#fee2e2', border: '1px solid #ef4444', borderRadius: '4px', color: '#b91c1c' }}>
          <strong>Conflict (409):</strong> {conflictError}
        </div>
      )}

      <div className="actions" style={{ marginTop: '1.25rem', justifyContent: 'flex-end' }}>
        <button type="button" className="secondary" onClick={onClose} disabled={retireMutation.isPending}>
          Cancel
        </button>
        <button
          type="button"
          className="danger"
          disabled={retireMutation.isPending}
          onClick={() => {
            setConflictError(null);
            retireMutation.mutate({ path: { id: skill.id } });
          }}
        >
          {retireMutation.isPending ? 'Retiring...' : 'Confirm retirement'}
        </button>
      </div>
    </ModalDialog>
  );
}

// -------------------------------------------------------------
// Grant Staff Skill Modal
// -------------------------------------------------------------

function GrantStaffSkillModal({
  staffId,
  staffName,
  onClose,
  onSuccess,
}: {
  staffId: string;
  staffName: string;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const skillsQuery = useQuery(listSkillsOptions());
  const availableSkills = skillsQuery.data ?? [];

  const [selectedSkillId, setSelectedSkillId] = useState('');
  const [validFrom, setValidFrom] = useState('');
  const [expiresAt, setExpiresAt] = useState('');

  const grantMutation = useMutation({
    ...grantStaffSkillMutation(),
    onSuccess: (data) => {
      toast.success(`Skill "${data.skill_name}" granted to ${staffName}.`);
      onSuccess();
      onClose();
    },
    onError: (err) => {
      const msg =
        (err as { detail?: string; title?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Failed to grant skill.';
      toast.error(msg);
    },
  });

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!selectedSkillId) {
      toast.error('Please select a skill.');
      return;
    }

    const payload: GrantStaffSkillRequest = {
      skill_id: selectedSkillId,
      ...(validFrom ? { valid_from: validFrom } : {}),
      ...(expiresAt ? { expires_at: new Date(expiresAt).toISOString() } : {}),
    };

    grantMutation.mutate({
      path: { id: staffId },
      body: payload,
    });
  }

  return (
    <ModalDialog title={`Grant skill: ${staffName}`} onClose={onClose} maxWidth="32rem">
      <form onSubmit={handleSubmit}>
        <div>
          <label htmlFor="grant-skill-select">Select skill qualification *</label>
          <select
            id="grant-skill-select"
            required
            value={selectedSkillId}
            onChange={(e) => setSelectedSkillId(e.target.value)}
          >
            <option value="">-- Choose skill --</option>
            {availableSkills.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </select>
        </div>

        <div className="row" style={{ marginTop: '0.85rem' }}>
          <div>
            <label htmlFor="grant-valid-from">Valid from (optional)</label>
            <input
              id="grant-valid-from"
              type="date"
              value={validFrom}
              onChange={(e) => setValidFrom(e.target.value)}
            />
          </div>
          <div>
            <label htmlFor="grant-expires-at">Expires at (optional)</label>
            <input
              id="grant-expires-at"
              type="date"
              value={expiresAt}
              onChange={(e) => setExpiresAt(e.target.value)}
            />
            <span className="muted" style={{ fontSize: '0.75rem' }}>
              Leave empty for permanent qualification
            </span>
          </div>
        </div>

        <div className="actions" style={{ marginTop: '1.25rem', justifyContent: 'flex-end' }}>
          <button type="button" className="secondary" onClick={onClose} disabled={grantMutation.isPending}>
            Cancel
          </button>
          <button type="submit" disabled={grantMutation.isPending || !selectedSkillId}>
            {grantMutation.isPending ? 'Granting...' : 'Grant qualification'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}

// -------------------------------------------------------------
// Revoke Staff Skill Confirmation Modal
// -------------------------------------------------------------

function RevokeStaffSkillConfirmModal({
  revoking,
  onClose,
  onSuccess,
}: {
  revoking: { staffId: string; staffName: string; skillId: string; skillName: string };
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [affectedAllocations, setAffectedAllocations] = useState<AllocationSummaryDto[] | null>(null);

  const revokeMutation = useMutation({
    ...revokeStaffSkillMutation(),
    onSuccess: (data) => {
      if (data.affected_allocations && data.affected_allocations.length > 0) {
        setAffectedAllocations(data.affected_allocations);
        toast.warning(
          `Skill revoked. ${data.affected_allocations.length} upcoming shift allocation(s) were affected.`,
        );
      } else {
        toast.success(`Skill "${revoking.skillName}" revoked from ${revoking.staffName}.`);
        onSuccess();
        onClose();
      }
    },
    onError: (err) => {
      const msg =
        (err as { detail?: string; title?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Failed to revoke skill.';
      toast.error(msg);
    },
  });

  return (
    <ModalDialog title={`Revoke skill: ${revoking.skillName}`} onClose={onClose} maxWidth="34rem">
      {affectedAllocations ? (
        <div>
          <div className="stub-note" style={{ borderColor: 'var(--warning, #eab308)', color: '#854d0e', background: '#fefce8' }}>
            <strong>Skill qualification revoked successfully.</strong>
            <p style={{ margin: '0.25rem 0 0 0' }}>
              The following {affectedAllocations.length} upcoming shift allocation{affectedAllocations.length === 1 ? '' : 's'} require this qualification and were affected:
            </p>
          </div>

          <table style={{ marginTop: '0.85rem' }}>
            <thead>
              <tr>
                <th>Date</th>
                <th>Time</th>
                <th>Ward</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {affectedAllocations.map((a) => (
                <tr key={a.allocation_id}>
                  <td><strong>{a.date}</strong></td>
                  <td>{a.start_time} - {a.end_time}</td>
                  <td>{a.ward_name}</td>
                  <td><span className="badge">{a.status}</span></td>
                </tr>
              ))}
            </tbody>
          </table>

          <div className="actions" style={{ marginTop: '1.25rem', justifyContent: 'flex-end' }}>
            <button
              type="button"
              onClick={() => {
                onSuccess();
                onClose();
              }}
            >
              Acknowledge & Close
            </button>
          </div>
        </div>
      ) : (
        <div>
          <div className="stub-note" style={{ borderColor: 'var(--danger)', color: 'var(--danger)', background: '#fdf2f2' }}>
            <strong>Warning:</strong> Revoking this qualification will immediately disqualify <strong>{revoking.staffName}</strong> from ward shifts requiring this skill.
          </div>

          <p style={{ marginTop: '0.85rem' }}>
            Are you sure you want to revoke <strong>{revoking.skillName}</strong> from <strong>{revoking.staffName}</strong>?
          </p>

          <div className="actions" style={{ marginTop: '1.25rem', justifyContent: 'flex-end' }}>
            <button type="button" className="secondary" onClick={onClose} disabled={revokeMutation.isPending}>
              Cancel
            </button>
            <button
              type="button"
              className="danger"
              disabled={revokeMutation.isPending}
              onClick={() => {
                revokeMutation.mutate({
                  path: {
                    staffId: revoking.staffId,
                    skillId: revoking.skillId,
                  },
                });
              }}
            >
              {revokeMutation.isPending ? 'Revoking...' : 'Confirm revocation'}
            </button>
          </div>
        </div>
      )}
    </ModalDialog>
  );
}
