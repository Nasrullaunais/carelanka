import { lazy, Suspense, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Button, Card, CardContent, CardTitle } from '@heroui/react';
import type { AmbulanceSummary } from '../../../services/api/generated';
import { listAmbulancesOptions, listEmergencyCallsOptions } from '../../../services/api/generated/@tanstack/react-query.gen';
import { DataTable, type DataTableColumn } from '../../../components/ui/data-table';
import { QueryState } from '../../../components/ui/query-state';
import { PaginationControls } from '../../../components/ui/pagination-controls';
import { StatusChip } from '../../../components/ui/status-chip';
import { ambulanceStatusLabels, ambulanceStatusTones, formatTimestamp } from '../domain';
import { CrewManagement } from './crew-management';

const FleetMap = lazy(() => import('./fleet-map').then((module) => ({ default: module.FleetMap })));

export function FleetBoard() {
  const [selected, setSelected] = useState<AmbulanceSummary>();
  const [view, setView] = useState<'board' | 'map'>('board');
  const [page, setPage] = useState(1);
  const fleet = useQuery({ ...listAmbulancesOptions({ query: { page, pageSize: 25 } }), refetchInterval: 5_000 });
  const calls = useQuery({ ...listEmergencyCallsOptions({ query: { page: 1, pageSize: 25 } }), refetchInterval: 5_000 });
  const columns: Array<DataTableColumn<AmbulanceSummary>> = [
    { key: 'registration', header: 'Ambulance', cell: (ambulance) => ambulance.registration_number },
    { key: 'status', header: 'Status', cell: (ambulance) => <StatusChip tone={ambulanceStatusTones[ambulance.status]}>{ambulanceStatusLabels[ambulance.status]}</StatusChip> },
    { key: 'crew', header: 'Crew', cell: (ambulance) => `${ambulance.current_crew_count ?? 0}/${ambulance.required_crew_count ?? 2}` },
    { key: 'location', header: 'Location updated', cell: (ambulance) => <span>{formatTimestamp(ambulance.location_updated_at)}{ambulance.eligibility_block_reasons?.includes('stale_location') ? ' · stale' : ''}{ambulance.current_latitude != null && ambulance.current_longitude != null && <> · <a className="text-accent underline" href={`https://www.google.com/maps?q=${ambulance.current_latitude},${ambulance.current_longitude}`} target="_blank" rel="noreferrer">Map</a></>}</span> },
    { key: 'dispatch', header: 'Active dispatch', cell: (ambulance) => ambulance.active_dispatch
      ? `${ambulance.active_dispatch.call_priority ?? 'Unknown priority'} · ${ambulance.active_dispatch.status ?? 'Unknown status'} · ${formatTimestamp(ambulance.active_dispatch.dispatched_at)}`
      : 'None' },
    { key: 'divertible', header: 'Divertible', cell: (ambulance) => ambulance.is_divertible ? 'Yes' : 'No' },
    { key: 'action', header: 'Crew actions', cell: (ambulance) => <Button size="sm" variant="outline" onPress={() => setSelected(ambulance)}>Manage crew</Button> },
  ];

  return (
    <div className="flex flex-col gap-4">
      <Card><CardContent className="flex flex-col gap-3">
        <div className="flex items-center justify-between gap-3"><CardTitle>Fleet board</CardTitle><div className="flex gap-2"><Button size="sm" variant={view === 'board' ? 'primary' : 'outline'} onPress={() => setView('board')}>Board</Button><Button size="sm" variant={view === 'map' ? 'primary' : 'outline'} onPress={() => setView('map')}>Map</Button></div></div>
        <QueryState query={fleet} errorContext="Could not load the ambulance fleet." isEmpty={(data) => data.items.length === 0} emptyMessage="No ambulances are registered.">
          {(fleetData) => view === 'board' ? (
            <DataTable ariaLabel="Ambulance fleet" rows={fleetData.items} columns={columns} rowKey={(row) => row.id} rowText={(row) => row.registration_number} pagination={{ page, totalPages: fleetData.total_pages, onPageChange: setPage }} />
          ) : (
            <QueryState query={calls} errorContext="Could not load calls for the fleet map.">
              {(callData) => <>
                <Suspense fallback={<div className="h-[28rem] animate-pulse rounded-xl bg-default-100" aria-label="Loading fleet map" />}>
                  <FleetMap ambulances={fleetData.items} calls={callData.items.filter((call) => call.status !== 'completed' && call.status !== 'cancelled')} selectedId={selected?.id} onSelect={(id) => setSelected(fleetData.items.find((item) => item.id === id))} />
                </Suspense>
                <PaginationControls label="Ambulance fleet" page={page} totalPages={fleetData.total_pages} onPageChange={setPage} />
              </>}
            </QueryState>
          )}
        </QueryState>
      </CardContent></Card>
      {selected && <CrewManagement ambulanceId={selected.id} registrationNumber={selected.registration_number} />}
    </div>
  );
}
