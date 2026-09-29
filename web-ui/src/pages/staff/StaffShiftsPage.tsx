import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  listShiftsOptions,
  listWardsOptions,
} from '../../services/api/generated/@tanstack/react-query.gen';
import type { CoverageStatus, ShiftDetailDto, ShiftSummaryDto, StaffRole } from '../../services/api/generated';
import { useSession } from '../../services/auth/useSession';
import { canManageStaff, canViewStaff } from '../../types/permissions';
import { PAGE_SIZE } from './shifts/constants';
import { ShiftFilterBar } from './shifts/ShiftFilterBar';
import { ShiftTable } from './shifts/ShiftTable';
import { CreateShiftModal } from './shifts/CreateShiftModal';
import { BulkShiftModal } from './shifts/BulkShiftModal';
import { ShiftDetailModal } from './shifts/ShiftDetailModal';
import { EditShiftModal } from './shifts/EditShiftModal';
import { CancelShiftModal } from './shifts/CancelShiftModal';

export function StaffShiftsPage() {
  const session = useSession();
  const queryClient = useQueryClient();
  const role = session?.principal.role;

  const canView = canViewStaff(role);
  const canManage = canManageStaff(role);

  // Filter state
  const [selectedWardId, setSelectedWardId] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [selectedRole, setSelectedRole] = useState<StaffRole | ''>('');
  const [selectedCoverageStatus, setSelectedCoverageStatus] = useState<CoverageStatus | ''>('');
  const [page, setPage] = useState(1);

  // Modal state
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showBulkModal, setShowBulkModal] = useState(false);
  const [detailShiftId, setDetailShiftId] = useState<string | null>(null);
  const [editShift, setEditShift] = useState<ShiftDetailDto | ShiftSummaryDto | null>(null);
  const [cancelShiftTarget, setCancelShiftTarget] = useState<ShiftSummaryDto | ShiftDetailDto | null>(null);

  // Queries
  const wardsQuery = useQuery({
    ...listWardsOptions({ query: { isActive: true } }),
    enabled: canView,
  });

  const shiftsQuery = useQuery({
    ...listShiftsOptions({
      query: {
        ...(selectedWardId ? { wardId: selectedWardId } : {}),
        ...(fromDate ? { from: fromDate } : {}),
        ...(toDate ? { to: toDate } : {}),
        ...(selectedRole ? { role: selectedRole } : {}),
        ...(selectedCoverageStatus ? { coverageStatus: selectedCoverageStatus } : {}),
        page,
        pageSize: PAGE_SIZE,
      },
    }),
    enabled: canView,
  });

  const invalidateShiftQueries = () => {
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
  };

  function handleResetFilters() {
    setSelectedWardId('');
    setFromDate('');
    setToDate('');
    setSelectedRole('');
    setSelectedCoverageStatus('');
    setPage(1);
  }

  if (!canView) {
    return (
      <>
        <h1>Staff shifts &amp; roster</h1>
        <p className="empty">
          Your role cannot view staff shifts. Hospital administrators and duty managers have access.
        </p>
      </>
    );
  }

  const wards = wardsQuery.data ?? [];
  const paged = shiftsQuery.data;
  const shiftsList = paged?.items ?? [];
  const totalPages = paged?.total_pages ?? 1;

  return (
    <>
      {/* Page header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '1rem', marginBottom: '0.5rem' }}>
        <div>
          <h1>Staff shifts &amp; roster</h1>
          <p className="muted">
            Ward shift scheduling, bulk shift planning, headcount requirements, and coverage tracking.
            {!canManage && ' (Duty Manager: view-only mode)'}
          </p>
        </div>

        <div className="actions">
          <Link to="/staff" className="secondary button" style={{ textDecoration: 'none' }}>
            Staff directory
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

          {canManage && (
            <>
              <button type="button" className="secondary" onClick={() => setShowBulkModal(true)}>
                Bulk plan shifts
              </button>
              <button type="button" onClick={() => setShowCreateModal(true)}>
                + Add shift
              </button>
            </>
          )}
        </div>
      </div>

      {/* Filter bar */}
      <ShiftFilterBar
        wards={wards}
        selectedWardId={selectedWardId}
        onWardChange={(v) => { setSelectedWardId(v); setPage(1); }}
        fromDate={fromDate}
        onFromDateChange={(v) => { setFromDate(v); setPage(1); }}
        toDate={toDate}
        onToDateChange={(v) => { setToDate(v); setPage(1); }}
        selectedRole={selectedRole}
        onRoleChange={(v) => { setSelectedRole(v); setPage(1); }}
        selectedCoverageStatus={selectedCoverageStatus}
        onCoverageStatusChange={(v) => { setSelectedCoverageStatus(v); setPage(1); }}
        onReset={handleResetFilters}
      />

      {/* Shifts table */}
      <ShiftTable
        shifts={shiftsList}
        totalItems={paged?.total_items}
        totalPages={totalPages}
        page={page}
        isFetching={shiftsQuery.isFetching}
        isPending={shiftsQuery.isPending}
        isError={shiftsQuery.isError}
        error={shiftsQuery.error instanceof Error ? shiftsQuery.error : null}
        canManage={canManage}
        onViewShift={setDetailShiftId}
        onEditShift={setEditShift}
        onCancelShift={setCancelShiftTarget}
        onCreateShift={() => setShowCreateModal(true)}
        onPageChange={setPage}
      />

      {/* Modals */}
      {showCreateModal && (
        <CreateShiftModal
          wards={wards}
          onClose={() => setShowCreateModal(false)}
          onSuccess={() => { setShowCreateModal(false); invalidateShiftQueries(); }}
        />
      )}

      {showBulkModal && (
        <BulkShiftModal
          wards={wards}
          onClose={() => setShowBulkModal(false)}
          onSuccess={() => { setShowBulkModal(false); invalidateShiftQueries(); }}
        />
      )}

      {detailShiftId && (
        <ShiftDetailModal
          shiftId={detailShiftId}
          canManage={canManage}
          onClose={() => setDetailShiftId(null)}
          onEdit={(shift) => { setDetailShiftId(null); setEditShift(shift); }}
          onCancel={(shift) => { setDetailShiftId(null); setCancelShiftTarget(shift); }}
        />
      )}

      {editShift && (
        <EditShiftModal
          shift={editShift}
          wards={wards}
          onClose={() => setEditShift(null)}
          onSuccess={() => { setEditShift(null); invalidateShiftQueries(); }}
        />
      )}

      {cancelShiftTarget && (
        <CancelShiftModal
          shift={cancelShiftTarget}
          onClose={() => setCancelShiftTarget(null)}
          onSuccess={() => { setCancelShiftTarget(null); invalidateShiftQueries(); }}
        />
      )}
    </>
  );
}
