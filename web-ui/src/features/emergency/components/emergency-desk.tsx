import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button } from '@heroui/react';
import { toast } from 'sonner';
import { createEmergencyCallMutation, getEmergencyCallOptions, listAmbulancesOptions, listEmergencyCallsOptions } from '../../../services/api/generated/@tanstack/react-query.gen';
import type { CreateEmergencyCallRequest } from '../../../services/api/generated';
import { nextCallToOpen } from '../domain';
import { invalidateEmergencyQueries } from '../query-invalidation';
import { CallBoard } from './call-board';
import { CallDetail } from './call-detail';
import { LogCallDialog } from './log-call-dialog';

const PAGE_SIZE = 25;
const REFRESH_MS = 5_000;
const CHECKING_REFRESH_MS = 1_500;

export function EmergencyDesk({ selectedCallId, onSelectCall, onCloseCall }: {
  selectedCallId?: string;
  onSelectCall: (id: string) => void;
  onCloseCall: () => void;
}) {
  const queryClient = useQueryClient();
  const [filter, setFilter] = useState<'received' | 'all'>('received');
  const [ambulancePage, setAmbulancePage] = useState(1);
  const [page, setPage] = useState(1);
  const [logCallOpen, setLogCallOpen] = useState(false);
  const calls = useQuery({
    ...listEmergencyCallsOptions({ query: { ...(filter === 'received' ? { status: 'received' as const } : {}), page, pageSize: PAGE_SIZE, sortBy: 'priority', sortDir: 'desc' } }),
    refetchInterval: REFRESH_MS,
  });
  const selectedCall = useQuery({
    ...getEmergencyCallOptions({ path: { id: selectedCallId ?? '' } }),
    enabled: Boolean(selectedCallId),
    refetchInterval: (query) => query.state.data?.latest_proposal?.status === 'pending' ? CHECKING_REFRESH_MS : REFRESH_MS,
  });
  const ambulances = useQuery({
    ...listAmbulancesOptions({
      query: {
        page: ambulancePage,
        pageSize: PAGE_SIZE,
        ...(selectedCall.data?.latitude != null && selectedCall.data.longitude != null
          ? { nearToLatitude: selectedCall.data.latitude, nearToLongitude: selectedCall.data.longitude, sortBy: 'distance' as const, sortDir: 'asc' as const }
          : {}),
      },
    }),
    enabled: Boolean(selectedCallId),
    refetchInterval: REFRESH_MS,
  });
  const createCall = useMutation({
    ...createEmergencyCallMutation(),
    onSuccess: (call) => {
      setLogCallOpen(false);
      if (call.id) selectCall(call.id);
      toast.success('Emergency call logged.');
      void invalidateEmergencyQueries(queryClient);
    },
  });

  function selectCall(id: string) {
    setAmbulancePage(1);
    onSelectCall(id);
  }

  function openNextCall() {
    const next = selectedCallId ? nextCallToOpen(calls.data?.items, selectedCallId) : undefined;
    if (next) selectCall(next);
    else onCloseCall();
  }

  return (
    <div className={selectedCallId ? 'emergency-desk emergency-desk-open' : 'emergency-desk'}>
      <section className="emergency-desk-list flex flex-col gap-4" aria-labelledby="live-call-heading">
        <div className="flex items-center justify-between gap-3">
          <h2 id="live-call-heading" className="m-0">Live calls</h2>
          <Button onPress={() => setLogCallOpen(true)}>Log emergency call</Button>
        </div>
        <CallBoard filter={filter} onFilterChange={(value) => { setFilter(value); setPage(1); }} calls={calls.data?.items} selectedId={selectedCallId} isLoading={calls.isPending} error={calls.error} onRetry={() => void calls.refetch()} onSelect={selectCall} page={page} totalPages={calls.data?.total_pages ?? 1} onPageChange={setPage} />
      </section>
      {selectedCallId
        ? (
          <section className="emergency-desk-call card" aria-label="Open call">
            <Button className="emergency-desk-back" size="sm" variant="ghost" onPress={onCloseCall}>Back to calls</Button>
            <CallDetail key={selectedCallId} callId={selectedCallId} query={selectedCall} ambulances={ambulances} onDispatched={openNextCall} ambulancePage={ambulancePage} onAmbulancePageChange={setAmbulancePage} />
          </section>
        )
        : (
          <section className="emergency-desk-call emergency-desk-placeholder workflow-empty" aria-label="Open call">
            <h2>Select a call</h2>
            <p className="muted">Its details, map and ambulance recommendation open here.</p>
          </section>
        )}
      <LogCallDialog isOpen={logCallOpen} isPending={createCall.isPending} onOpenChange={setLogCallOpen} onSubmit={(body: CreateEmergencyCallRequest) => createCall.mutate({ body })} />
    </div>
  );
}
