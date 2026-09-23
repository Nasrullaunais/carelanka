import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, CardContent, CardTitle } from '@heroui/react';
import { toast } from 'sonner';
import type { AmbulanceDetail, AmbulanceStatus, AmbulanceSummary } from '../../../services/api/generated';
import {
  createAmbulanceMutation,
  getAmbulanceOptions,
  listAmbulancesOptions,
  reinstateAmbulanceMutation,
  retireAmbulanceMutation,
  updateAmbulanceMutation,
} from '../../../services/api/generated/@tanstack/react-query.gen';
import { ConfirmDialog } from '../../../components/ui/confirm-dialog';
import { DataTable, type DataTableColumn } from '../../../components/ui/data-table';
import { QueryState } from '../../../components/ui/query-state';
import { StatusChip } from '../../../components/ui/status-chip';
import { isConflict } from '../../../services/api/errors';
import { ambulanceStatusLabels, ambulanceStatusTones } from '../domain';
import { invalidateEmergencyQueries } from '../query-invalidation';
import { AmbulanceDialog } from './ambulance-dialog';
import { CrewManagement } from './crew-management';
import { ActionDialog } from '../../../components/ui/action-dialog';

type DialogState = { kind: 'create' } | { kind: 'edit'; ambulance: AmbulanceDetail };
type ConfirmAction = { kind: 'retire' | 'reinstate'; ambulance: AmbulanceSummary };

export function AmbulanceRegister() {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<AmbulanceStatus | ''>('');
  const [page, setPage] = useState(1);
  const [dialog, setDialog] = useState<DialogState>();
  const [loadingEditId, setLoadingEditId] = useState<string>();
  const [crewAmbulance, setCrewAmbulance] = useState<AmbulanceSummary>();
  const [confirm, setConfirm] = useState<ConfirmAction>();
  const list = useQuery(listAmbulancesOptions({ query: { page, pageSize: 25, includeRetired: true, status: status || undefined } }));
  const refresh = () => invalidateEmergencyQueries(queryClient);
  const mutationHandlers = (success: string) => ({
    onSuccess: () => { setDialog(undefined); setConfirm(undefined); toast.success(success); void refresh(); },
    onError: (error: Parameters<typeof isConflict>[0]) => {
      if (isConflict(error)) {
        toast.error('The ambulance changed or has an active run. The register has been refreshed.');
        void refresh();
      }
    },
  });
  const create = useMutation({ ...createAmbulanceMutation(), ...mutationHandlers('Ambulance added.') });
  const update = useMutation({ ...updateAmbulanceMutation(), ...mutationHandlers('Ambulance updated.') });
  const retire = useMutation({ ...retireAmbulanceMutation(), ...mutationHandlers('Ambulance retired.') });
  const reinstate = useMutation({ ...reinstateAmbulanceMutation(), ...mutationHandlers('Ambulance reinstated.') });
  const columns: Array<DataTableColumn<AmbulanceSummary>> = [
    { key: 'registration', header: 'Ambulance', cell: (item) => item.registration_number },
    { key: 'status', header: 'Status', cell: (item) => <StatusChip tone={ambulanceStatusTones[item.status]}>{isRetired(item) ? 'Retired' : ambulanceStatusLabels[item.status]}</StatusChip> },
    { key: 'actions', header: 'Actions', cell: (item) => <div className="flex flex-wrap gap-2" onClick={(event) => event.stopPropagation()}>
      <Button size="sm" variant="outline" isDisabled={loadingEditId != null} onPress={() => void openEdit(item.id)}>Edit</Button>
      <Button size="sm" variant="outline" onPress={() => setCrewAmbulance(item)}>Crew</Button>
      <Button size="sm" variant={isRetired(item) ? 'outline' : 'danger'} onPress={() => setConfirm({ kind: isRetired(item) ? 'reinstate' : 'retire', ambulance: item })}>{isRetired(item) ? 'Reinstate' : 'Retire'}</Button>
    </div> },
  ];

  async function openEdit(id: string) {
    setLoadingEditId(id);
    try {
      const ambulance = await queryClient.fetchQuery(getAmbulanceOptions({ path: { id } }));
      setDialog({ kind: 'edit', ambulance });
    } finally {
      setLoadingEditId(undefined);
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <Card><CardContent className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center justify-between gap-3"><CardTitle>Ambulance register</CardTitle><Button onPress={() => setDialog({ kind: 'create' })}>Add ambulance</Button></div>
        <label className="flex max-w-xs flex-col gap-1 text-sm">Status filter<select className="rounded-lg border border-default-200 bg-background px-3 py-2" value={status} onChange={(event) => { setStatus(event.target.value as AmbulanceStatus | ''); setPage(1); }}>
          <option value="">All statuses</option>{Object.entries(ambulanceStatusLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select></label>
        <QueryState query={list} errorContext="Could not load the ambulance register." isEmpty={(data) => data.items.length === 0} emptyMessage="No ambulances are registered.">
          {(data) => <DataTable ariaLabel="Ambulance register" rows={data.items} columns={columns} rowKey={(row) => row.id} rowText={(row) => row.registration_number} pagination={{ page, totalPages: data.total_pages, onPageChange: setPage }} />}
        </QueryState>
      </CardContent></Card>
      <ActionDialog title={`Manage crew · ${crewAmbulance?.registration_number ?? ''}`} isOpen={crewAmbulance != null} onClose={() => setCrewAmbulance(undefined)}>
        {crewAmbulance && <CrewManagement ambulanceId={crewAmbulance.id} registrationNumber={crewAmbulance.registration_number} />}
      </ActionDialog>
      <AmbulanceDialog
        isOpen={dialog != null}
        ambulance={dialog?.kind === 'edit' ? dialog.ambulance : undefined}
        isPending={create.isPending || update.isPending}
        onOpenChange={(open) => !open && setDialog(undefined)}
        onCreate={(body) => create.mutate({ body })}
        onUpdate={(body) => dialog?.kind === 'edit' && update.mutate({ path: { id: dialog.ambulance.id }, body })}
      />
      <ConfirmDialog isOpen={confirm != null} onOpenChange={(open) => !open && setConfirm(undefined)} title={`${confirm?.kind === 'retire' ? 'Retire' : 'Reinstate'} ${confirm?.ambulance.registration_number ?? 'ambulance'}?`} description={confirm?.kind === 'retire' ? 'The ambulance will no longer be available for dispatch.' : 'The ambulance will return to active fleet management.'} confirmLabel={confirm?.kind === 'retire' ? 'Retire ambulance' : 'Reinstate ambulance'} tone={confirm?.kind === 'retire' ? 'danger' : 'accent'} isPending={retire.isPending || reinstate.isPending} onConfirm={() => {
        if (!confirm) return;
        if (confirm.kind === 'retire') retire.mutate({ path: { id: confirm.ambulance.id }, body: { reason: null } });
        else reinstate.mutate({ path: { id: confirm.ambulance.id } });
      }} />
    </div>
  );
}

function isRetired(ambulance: AmbulanceSummary): boolean {
  return ambulance.is_active === false;
}
