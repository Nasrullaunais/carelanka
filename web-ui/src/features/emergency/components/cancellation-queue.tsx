import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, CardContent, CardTitle } from '@heroui/react';
import { toast } from 'sonner';
import type { CancellationRequestStatus, EmergencyCancellationRequest } from '../../../services/api/generated';
import {
  approveEmergencyCancellationRequestMutation,
  listEmergencyCancellationRequestsOptions,
  listEmergencyCancellationRequestsQueryKey,
  rejectEmergencyCancellationRequestMutation,
} from '../../../services/api/generated/@tanstack/react-query.gen';
import { ConfirmDialog } from '../../../components/ui/confirm-dialog';
import { QueryState } from '../../../components/ui/query-state';
import { PaginationControls } from '../../../components/ui/pagination-controls';
import { ReasonDialog } from '../../../components/ui/reason-dialog';
import { StatusChip } from '../../../components/ui/status-chip';
import { isConflict, type ApiProblem } from '../../../services/api/errors';
import {
  callStatusLabels,
  callStatusTones,
  cancellationStatusLabels,
  formatTimestamp,
  priorityLabels,
  priorityTones,
} from '../domain';
import { invalidateEmergencyQueries } from '../query-invalidation';

const cancellationStatusTones = {
  pending: 'warning',
  approved: 'success',
  rejected: 'danger',
} as const satisfies Record<CancellationRequestStatus, 'warning' | 'success' | 'danger'>;

export function CancellationQueue() {
  const [page, setPage] = useState(1);
  const query = useQuery({
    ...listEmergencyCancellationRequestsOptions({ query: { Page: page, PageSize: 25 } }),
    refetchInterval: 5_000,
  });

  const requests = useMemo(
    () => [...(query.data?.items ?? [])].sort((left, right) => {
      const leftPending = left.status === 'pending' ? 0 : 1;
      const rightPending = right.status === 'pending' ? 0 : 1;
      return leftPending - rightPending || Date.parse(right.requested_at ?? '') - Date.parse(left.requested_at ?? '');
    }),
    [query.data?.items],
  );

  return (
    <QueryState
      query={query}
      isEmpty={(data) => data.items.length === 0}
      emptyMessage="No cancellation requests to review."
      errorContext="Could not load cancellation requests."
      skeletonRows={4}
    >
      {(data) => <div className="flex flex-col gap-4">
        {requests.map((request) => <CancellationCard key={request.emergency_call_id} request={request} />)}
        <PaginationControls label="Cancellation requests" page={page} totalPages={data.total_pages} onPageChange={setPage} />
      </div>}
    </QueryState>
  );
}

function CancellationCard({ request }: { request: EmergencyCancellationRequest }) {
  const callId = request.emergency_call_id ?? '';
  const queryClient = useQueryClient();
  const [approveOpen, setApproveOpen] = useState(false);
  const [rejectOpen, setRejectOpen] = useState(false);

  function complete(message: string) {
    setApproveOpen(false);
    setRejectOpen(false);
    toast.success(message);
    void invalidateEmergencyQueries(queryClient);
    void queryClient.invalidateQueries({ queryKey: listEmergencyCancellationRequestsQueryKey() });
  }

  function failed(error: ApiProblem) {
    if (isConflict(error)) {
      toast.error('This cancellation request was already reviewed. The queue has been refreshed.');
    }
    void queryClient.invalidateQueries({ queryKey: listEmergencyCancellationRequestsQueryKey() });
  }

  const approve = useMutation({
    ...approveEmergencyCancellationRequestMutation(),
    onSuccess: () => complete('Cancellation approved. The active dispatch has been recalled.'),
    onError: (error) => failed(error as ApiProblem),
  });
  const reject = useMutation({
    ...rejectEmergencyCancellationRequestMutation(),
    onSuccess: () => complete('Cancellation request rejected.'),
    onError: (error) => failed(error as ApiProblem),
  });

  if (callId === '') return null;
  const status = request.status ?? 'pending';
  const priority = request.call_priority ?? 'high';
  const callStatus = request.call_status ?? 'received';
  const waitingMinutes = request.call_created_at == null
    ? undefined
    : Math.max(0, Math.floor((Date.now() - Date.parse(request.call_created_at)) / 60_000));
  const pending = approve.isPending || reject.isPending;

  return (
    <Card>
      <CardContent className="flex flex-col gap-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <CardTitle>{request.caller_name ?? request.address_label ?? 'Emergency call'}</CardTitle>
            <p className="text-sm text-muted">Requested {formatTimestamp(request.requested_at)}</p>
          </div>
          <div className="flex flex-wrap gap-2">
            <StatusChip tone={priorityTones[priority]}>{priorityLabels[priority]}</StatusChip>
            <StatusChip tone={callStatusTones[callStatus]}>{callStatusLabels[callStatus]}</StatusChip>
            <StatusChip tone={cancellationStatusTones[status]}>{cancellationStatusLabels[status]}</StatusChip>
          </div>
        </div>
        <div>
          <p className="text-sm font-medium">Caller’s reason</p>
          <p>{request.reason?.trim() || 'No reason provided.'}</p>
        </div>
        <dl className="grid grid-cols-1 gap-2 text-sm sm:grid-cols-3">
          <div><dt className="text-muted">Waiting</dt><dd>{waitingMinutes == null ? 'Unknown' : `${waitingMinutes} min`}</dd></div>
          <div><dt className="text-muted">Assigned ambulance</dt><dd>{request.active_ambulance_registration ?? 'None'}</dd></div>
          <div><dt className="text-muted">Location</dt><dd>{request.address_label ?? 'Not recorded'}</dd></div>
        </dl>
        {status === 'pending' ? (
          <div className="flex flex-wrap gap-2">
            <Button variant="danger" isDisabled={pending} onPress={() => setApproveOpen(true)}>Approve cancellation</Button>
            <Button variant="outline" isDisabled={pending} onPress={() => setRejectOpen(true)}>Reject request</Button>
          </div>
        ) : request.review_notes ? (
          <div><p className="text-sm font-medium">Review notes</p><p>{request.review_notes}</p></div>
        ) : null}
      </CardContent>
      <ConfirmDialog
        isOpen={approveOpen}
        onOpenChange={setApproveOpen}
        title="Approve cancellation request?"
        description="This cancels the active dispatch and recalls the ambulance, making it available for other calls."
        confirmLabel={approve.isPending ? 'Approving…' : 'Approve and recall'}
        isPending={approve.isPending}
        onConfirm={() => approve.mutate({ path: { id: callId }, body: {} })}
      />
      <ReasonDialog
        isOpen={rejectOpen}
        onOpenChange={setRejectOpen}
        title="Reject cancellation request"
        description="Explain why the emergency response should continue. The patient can see this review result."
        notesLabel="Review notes"
        notesPlaceholder="Explain why the request cannot be approved"
        notesRequired
        confirmLabel={reject.isPending ? 'Rejecting…' : 'Reject request'}
        isPending={reject.isPending}
        onConfirm={({ notes }) => reject.mutate({ path: { id: callId }, body: { notes } })}
      />
    </Card>
  );
}
