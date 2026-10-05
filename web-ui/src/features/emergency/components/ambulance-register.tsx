import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button } from '@heroui/react';
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
import { ReasonDialog } from '../../../components/ui/reason-dialog';
import { DataTable, type DataTableColumn } from '../../../components/ui/data-table';
import { QueryState } from '../../../components/ui/query-state';
import { StatusChip } from '../../../components/ui/status-chip';
import { isConflict } from '../../../services/api/errors';
import { ambulanceStatusLabels, ambulanceStatusTones } from '../domain';
import { invalidateEmergencyQueries } from '../query-invalidation';
import { AmbulanceDialog } from './ambulance-dialog';
import { CrewManagement } from './crew-management';
import { ActionDialog } from '../../../components/ui/action-dialog';
import { AppSelect } from '../../../components/ui/app-select';

type DialogState = { kind: 'create' } | { kind: 'edit'; ambulance: AmbulanceDetail };

export function AmbulanceRegister() {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<AmbulanceStatus | ''>('');
  const [page, setPage] = useState(1);
  const [dialog, setDialog] = useState<DialogState>();
  const [loadingEditId, setLoadingEditId] = useState<string>();
  const [crewAmbulance, setCrewAmbulance] = useState<AmbulanceSummary>();
  const [retiring, setRetiring] = useState<AmbulanceSummary>();
  const [reinstating, setReinstating] = useState<AmbulanceSummary>();
  const list = useQuery(listAmbulancesOptions({ query: { page, pageSize: 25, includeRetired: true, status: status || undefined } }));
  const refresh = () => invalidateEmergencyQueries(queryClient);
  const mutationHandlers = (success: string) => ({
    onSuccess: () => { setDialog(undefined); setRetiring(undefined); setReinstating(undefined); toast.success(success); void refresh(); },
    onError: (error: Parameters<typeof isConflict>[0]) => {
      if (isConflict(error)) void refresh();
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
      {isRetired(item)
        ? <Button size="sm" variant="outline" onPress={() => setReinstating(item)}>Reinstate</Button>
        : item.active_dispatch_id == null && <Button size="sm" variant="danger-soft" onPress={() => setRetiring(item)}>Retire</Button>}
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
      <section className="flex flex-col gap-3" aria-labelledby="ambulance-register-heading">
        <div className="flex flex-wrap items-center justify-between gap-3"><h2 id="ambulance-register-heading" className="m-0">Ambulance register</h2><Button onPress={() => setDialog({ kind: 'create' })}>Add ambulance</Button></div>
        <AppSelect
          className="max-w-xs"
          label="Status filter"
          value={status}
          onValueChange={(value) => { setStatus(value as AmbulanceStatus | ''); setPage(1); }}
          options={[
            { value: '', label: 'All statuses' },
            ...Object.entries(ambulanceStatusLabels).map(([value, label]) => ({ value, label })),
          ]}
        />
        <QueryState query={list} errorContext="Could not load the ambulance register." isEmpty={(data) => data.items.length === 0} emptyMessage="No ambulances are registered.">
          {(data) => <DataTable ariaLabel="Ambulance register" rows={data.items} columns={columns} rowKey={(row) => row.id} rowText={(row) => row.registration_number} pagination={{ page, totalPages: data.total_pages, onPageChange: setPage }} />}
        </QueryState>
      </section>
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
      <ReasonDialog
        isOpen={retiring != null}
        onOpenChange={(open) => !open && setRetiring(undefined)}
        title={`Retire ${retiring?.registration_number ?? 'ambulance'}?`}
        description="The ambulance leaves the fleet and can no longer be dispatched. Its current crew are unassigned so they can join another ambulance."
        notesLabel="Reason"
        notesPlaceholder="For example: written off after an accident"
        notesRequired
        confirmLabel={retire.isPending ? 'Retiring…' : 'Retire ambulance'}
        isPending={retire.isPending}
        onConfirm={({ notes }) => retiring && notes && retire.mutate({ path: { id: retiring.id }, body: { reason: notes.trim() } })}
      />
      <ConfirmDialog
        isOpen={reinstating != null}
        onOpenChange={(open) => !open && setReinstating(undefined)}
        title={`Reinstate ${reinstating?.registration_number ?? 'ambulance'}?`}
        description="The ambulance returns to the fleet as available. Assign a crew before it can be dispatched."
        confirmLabel="Reinstate ambulance"
        tone="accent"
        isPending={reinstate.isPending}
        onConfirm={() => reinstating && reinstate.mutate({ path: { id: reinstating.id } })}
      />
    </div>
  );
}

function isRetired(ambulance: AmbulanceSummary): boolean {
  return ambulance.is_active === false;
}
