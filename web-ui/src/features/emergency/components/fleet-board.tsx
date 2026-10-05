import { lazy, Suspense, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { Button, Card, CardContent, CardTitle } from '@heroui/react';
import type { AmbulanceSummary, FleetMap, FleetMapAmbulance } from '../../../services/api/generated';
import { getFleetMapOptions, listAmbulancesOptions } from '../../../services/api/generated/@tanstack/react-query.gen';
import { DataTable, type DataTableColumn } from '../../../components/ui/data-table';
import { DetailField } from '../../../components/ui/detail-field';
import { QueryState } from '../../../components/ui/query-state';
import { StatusChip } from '../../../components/ui/status-chip';
import { ActionDialog } from '../../../components/ui/action-dialog';
import {
  ambulanceStatusLabels,
  ambulanceStatusTones,
  blockReasonLabels,
  dispatchStatusLabels,
  emergencyCallPath,
  formatAge,
  formatClock,
  formatTimestamp,
  priorityLabels,
} from '../domain';
import { CrewManagement } from './crew-management';

const FleetMapView = lazy(() => import('./fleet-map').then((module) => ({ default: module.FleetMap })));

const REFRESH_MS = 5_000;

type CrewTarget = { id: string; registration_number: string };

export function FleetBoard() {
  const navigate = useNavigate();
  const [crewTarget, setCrewTarget] = useState<CrewTarget>();
  const [view, setView] = useState<'board' | 'map'>('board');
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState<string>();
  const fleet = useQuery({ ...listAmbulancesOptions({ query: { page, pageSize: 25 } }), refetchInterval: REFRESH_MS, enabled: view === 'board' });
  const live = useQuery({ ...getFleetMapOptions(), refetchInterval: REFRESH_MS, enabled: view === 'map' });
  const columns: Array<DataTableColumn<AmbulanceSummary>> = [
    { key: 'registration', header: 'Ambulance', cell: (ambulance) => <span className="whitespace-nowrap font-semibold">{ambulance.registration_number}</span> },
    { key: 'status', header: 'Status', cell: (ambulance) => <StatusChip tone={ambulanceStatusTones[ambulance.status]}>{ambulanceStatusLabels[ambulance.status]}</StatusChip> },
    { key: 'crew', header: 'Crew', cell: (ambulance) => <CrewCount current={ambulance.current_crew_count} required={ambulance.required_crew_count} /> },
    { key: 'location', header: 'Last position', cell: (ambulance) => (
      <div className="flex flex-col">
        <span className="whitespace-nowrap" title={formatTimestamp(ambulance.location_updated_at)}>{formatClock(ambulance.location_updated_at)}</span>
        {ambulance.eligibility_block_reasons?.includes('stale_location') && <span className="text-xs text-danger">May be out of date</span>}
        {ambulance.current_latitude != null && ambulance.current_longitude != null && <a className="text-xs text-accent underline" href={`https://www.google.com/maps?q=${ambulance.current_latitude},${ambulance.current_longitude}`} target="_blank" rel="noreferrer">Show on map</a>}
      </div>
    ) },
    { key: 'dispatch', header: 'Active dispatch', cell: (ambulance) => {
      const run = ambulance.active_dispatch;
      if (!run) return <span className="text-muted">None</span>;
      const label = [run.call_priority && `${priorityLabels[run.call_priority]} call`, run.status && dispatchStatusLabels[run.status]].filter(Boolean).join(' · ');
      return (
        <div className="flex flex-col items-start">
          {run.emergency_call_id
            ? <button type="button" className="linklike" onClick={() => navigate(emergencyCallPath(run.emergency_call_id!))}>{label}</button>
            : <span>{label}</span>}
          <span className="text-xs text-muted">Sent at {formatClock(run.dispatched_at)}</span>
          {run.acknowledgement_overdue && <span className="text-xs text-danger">Crew has not accepted yet</span>}
        </div>
      );
    } },
    { key: 'divertible', header: 'Divertible', cell: (ambulance) => ambulance.is_divertible ? 'Yes' : 'No' },
    { key: 'action', header: 'Crew actions', cell: (ambulance) => <Button size="sm" variant="outline" onPress={() => setCrewTarget(ambulance)}>Manage crew</Button> },
  ];

  return (
    <div className="flex flex-col gap-4">
      <section className="flex flex-col gap-3" aria-labelledby="fleet-board-heading">
        <div className="flex items-center justify-between gap-3"><h2 id="fleet-board-heading" className="m-0">Fleet board</h2><div className="flex gap-2"><Button size="sm" variant={view === 'board' ? 'primary' : 'outline'} onPress={() => setView('board')}>Board</Button><Button size="sm" variant={view === 'map' ? 'primary' : 'outline'} onPress={() => setView('map')}>Map</Button></div></div>
        {view === 'board' ? (
          <QueryState query={fleet} errorContext="Could not load the ambulance fleet." isEmpty={(data) => data.items.length === 0} emptyMessage="No ambulances are registered.">
            {(data) => <DataTable ariaLabel="Ambulance fleet" rows={data.items} columns={columns} rowKey={(row) => row.id} rowText={(row) => row.registration_number} pagination={{ page, totalPages: data.total_pages, onPageChange: setPage }} />}
          </QueryState>
        ) : (
          <QueryState query={live} errorContext="Could not load the fleet map.">
            {(data) => (
              <div className="grid grid-cols-1 items-start gap-4 xl:grid-cols-[1fr_20rem]">
                <Suspense fallback={<div className="h-[32rem] animate-pulse rounded-xl bg-default-100" aria-label="Loading fleet map" />}>
                  <FleetMapView map={data} selectedAmbulanceId={selectedId} onSelectAmbulance={setSelectedId} onOpenCall={(id) => navigate(emergencyCallPath(id))} />
                </Suspense>
                <FleetSidePanel
                  map={data}
                  selectedId={selectedId}
                  onSelect={setSelectedId}
                  onOpenCall={(id) => navigate(emergencyCallPath(id))}
                  onManageCrew={(ambulance) => ambulance.id && setCrewTarget({ id: ambulance.id, registration_number: ambulance.registration_number ?? '' })}
                />
              </div>
            )}
          </QueryState>
        )}
      </section>
      <ActionDialog title={`Manage crew · ${crewTarget?.registration_number ?? ''}`} isOpen={crewTarget != null} onClose={() => setCrewTarget(undefined)}>
        {crewTarget && <CrewManagement ambulanceId={crewTarget.id} registrationNumber={crewTarget.registration_number} />}
      </ActionDialog>
    </div>
  );
}

function FleetSidePanel({ map, selectedId, onSelect, onOpenCall, onManageCrew }: {
  map: FleetMap;
  selectedId?: string;
  onSelect: (id: string | undefined) => void;
  onOpenCall: (id: string) => void;
  onManageCrew: (ambulance: FleetMapAmbulance) => void;
}) {
  const ambulances = map.ambulances ?? [];
  const selected = ambulances.find((ambulance) => ambulance.id === selectedId);
  const waiting = (map.calls ?? []).filter((call) => !call.assigned_ambulance_id).length;
  const unplaced = ambulances.filter((ambulance) => ambulance.latitude == null || ambulance.longitude == null).length;

  if (!selected) {
    return (
      <Card>
        <CardContent className="flex flex-col gap-2 text-sm">
          <CardTitle>On the map</CardTitle>
          <p>{ambulances.length} ambulances · {map.calls?.length ?? 0} open calls · {waiting} waiting for an ambulance</p>
          {unplaced > 0 && <p className="text-muted">{unplaced} ambulance{unplaced === 1 ? ' has' : 's have'} never sent a position and {unplaced === 1 ? 'is' : 'are'} not shown.</p>}
          <p className="text-muted">Faded squares have not sent a position for over {map.location_max_age_minutes ?? 5} minutes. Click an ambulance for details, or a call pin to open it.</p>
        </CardContent>
      </Card>
    );
  }

  const status = selected.status ?? 'available';
  return (
    <Card>
      <CardContent className="flex flex-col gap-3">
        <div className="flex items-center justify-between gap-2">
          <CardTitle>{selected.registration_number}</CardTitle>
          <StatusChip tone={ambulanceStatusTones[status]}>{ambulanceStatusLabels[status]}</StatusChip>
        </div>
        <DetailField label="Crew"><CrewCount current={selected.current_crew_count} required={selected.required_crew_count} /></DetailField>
        <DetailField label="Position updated">
          {formatAge(selected.location_updated_at)}
          {selected.location_is_stale && <span className="text-danger"> · may be out of date</span>}
        </DetailField>
        {selected.out_of_service_reason && status === 'out_of_service' && <DetailField label="Out of service because">{selected.out_of_service_reason}</DetailField>}
        {selected.active_dispatch_status && <DetailField label="Current run">{dispatchStatusLabels[selected.active_dispatch_status]}</DetailField>}
        {!selected.is_eligible && !selected.active_dispatch_id && (selected.eligibility_block_reasons?.length ?? 0) > 0 && (
          <DetailField label="Cannot be sent because">{selected.eligibility_block_reasons!.map((reason) => blockReasonLabels[reason]).join(' · ')}</DetailField>
        )}
        <div className="flex flex-wrap gap-2">
          {selected.active_call_id && <Button size="sm" onPress={() => onOpenCall(selected.active_call_id!)}>Open its call</Button>}
          <Button size="sm" variant="outline" onPress={() => onManageCrew(selected)}>Manage crew</Button>
          <Button size="sm" variant="ghost" onPress={() => onSelect(undefined)}>Close</Button>
        </div>
      </CardContent>
    </Card>
  );
}

function CrewCount({ current = 0, required = 2 }: { current?: number; required?: number }) {
  return current < required
    ? <span className="whitespace-nowrap text-danger">{current} · needs {required}</span>
    : <span className="whitespace-nowrap">{current} on board</span>;
}
