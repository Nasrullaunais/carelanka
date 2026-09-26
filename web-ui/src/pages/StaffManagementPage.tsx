import { useState } from 'react';
import type { FormEvent, ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  createStaffMemberMutation,
  deactivateStaffMemberMutation,
  getStaffMemberOptions,
  listSkillsOptions,
  listStaffOptions,
  lookupStaffMutation,
  reactivateStaffMemberMutation,
  updateStaffMemberMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type {
  CreateStaffMemberRequest,
  StaffLookupResult,
  StaffMemberDetailDto,
  StaffRole,
  StaffSummaryDto,
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

  // Modals state
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [detailStaffId, setDetailStaffId] = useState<string | null>(null);
  const [editStaffMember, setEditStaffMember] = useState<StaffMemberDetailDto | StaffSummaryDto | null>(null);
  const [deactivateStaff, setDeactivateStaff] = useState<{ id: string; name: string } | null>(null);
  const [reactivateStaff, setReactivateStaff] = useState<{ id: string; name: string } | null>(null);
  const [showLookupSection, setShowLookupSection] = useState(false);

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
      predicate: (query) =>
        (query.queryKey[0] as { _id?: string } | undefined)?._id === 'listStaff',
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
          <button
            type="button"
            className="secondary"
            onClick={() => setShowLookupSection((prev) => !prev)}
          >
            {showLookupSection ? 'Hide quick lookup' : 'Quick ID lookup'}
          </button>
          {canManage && (
            <button type="button" onClick={() => setShowCreateModal(true)}>
              + Add staff member
            </button>
          )}
        </div>
      </div>

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
}: {
  staffId: string;
  canManage: boolean;
  onClose: () => void;
  onEdit: (detail: StaffMemberDetailDto) => void;
  onDeactivate: (id: string, name: string) => void;
  onReactivate: (id: string, name: string) => void;
}) {
  const detailQuery = useQuery(getStaffMemberOptions({ path: { id: staffId } }));
  const detail = detailQuery.data;

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
            <h3>Skills & Certifications ({detail.skills?.length ?? 0})</h3>
            {detail.skills && detail.skills.length > 0 ? (
              <table style={{ marginTop: '0.4rem' }}>
                <thead>
                  <tr>
                    <th>Skill name</th>
                    <th>Status</th>
                    <th>Granted</th>
                    <th>Expires</th>
                  </tr>
                </thead>
                <tbody>
                  {detail.skills.map((s) => (
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
