import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button } from '@heroui/react';
import { toast } from 'sonner';
import { createEmergencyCallMutation, getEmergencyCallOptions, listAmbulancesOptions, listEmergencyCallsOptions } from '../../../services/api/generated/@tanstack/react-query.gen';
import type { CreateEmergencyCallRequest } from '../../../services/api/generated';
import { invalidateEmergencyQueries } from '../query-invalidation';
import { CallBoard } from './call-board';
import { CallDetail } from './call-detail';
import { ActionDialog } from '../../../components/ui/action-dialog';
import { LogCallDialog } from './log-call-dialog';

const PAGE_SIZE = 25;
const REFRESH_MS = 5_000;

export function EmergencyDesk({ onReviewProposal, initialCallId }: { onReviewProposal?: () => void; initialCallId?: string }) {
  const queryClient = useQueryClient();
  const [selectedCallId, setSelectedCallId] = useState<string | undefined>(initialCallId);
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
    refetchInterval: REFRESH_MS,
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
    refetchInterval: REFRESH_MS,
  });
  const createCall = useMutation({
    ...createEmergencyCallMutation(),
    onSuccess: (call) => {
      setLogCallOpen(false);
      setSelectedCallId(call.id);
      toast.success('Emergency call logged.');
      void invalidateEmergencyQueries(queryClient);
    },
  });

  return (
    <div className="flex flex-col gap-4">
      <section className="flex flex-col gap-4" aria-labelledby="live-call-heading">
          <div className="flex items-center justify-between gap-3"><h2 id="live-call-heading" className="m-0">Live call board</h2><Button onPress={() => setLogCallOpen(true)}>Log emergency call</Button></div>
          <CallBoard filter={filter} onFilterChange={(value) => { setFilter(value); setPage(1); }} calls={calls.data?.items} selectedId={selectedCallId} isLoading={calls.isPending} error={calls.error} onRetry={() => void calls.refetch()} onSelect={(id) => { setSelectedCallId(id); setAmbulancePage(1); }} page={page} totalPages={calls.data?.total_pages ?? 1} onPageChange={setPage} />
        </section>
      <ActionDialog title={`Emergency call · ${selectedCall.data?.caller_name ?? 'details'}`} isOpen={selectedCallId != null} onClose={() => setSelectedCallId(undefined)}>
        {selectedCallId && <CallDetail callId={selectedCallId} query={selectedCall} ambulances={ambulances} onReviewProposal={onReviewProposal} ambulancePage={ambulancePage} onAmbulancePageChange={setAmbulancePage} />}
      </ActionDialog>
      <LogCallDialog isOpen={logCallOpen} isPending={createCall.isPending} onOpenChange={setLogCallOpen} onSubmit={(body: CreateEmergencyCallRequest) => createCall.mutate({ body })} />
    </div>
  );
}
