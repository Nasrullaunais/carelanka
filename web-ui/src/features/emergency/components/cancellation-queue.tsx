import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button } from '@heroui/react';
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
import './cancellation-queue.css';

const cancellationStatusTones = {
  pending: 'warning',
  approved: 'success',
  rejected: 'danger',
} as const satisfies Record<CancellationRequestStatus, 'warning' | 'success' | 'danger'>;

function elapsedMinutes(iso?: string | null): string {
  if (!iso) return 'Unknown';
  const start = Date.parse(iso);
  if (Number.isNaN(start)) return 'Unknown';
  const minutes = Math.max(0, Math.floor((Date.now() - start) / 60_000));
  if (minutes < 60) return `${minutes} min`;
  return `${Math.floor(minutes / 60)} hr ${minutes % 60} min`;
}

export function CancellationQueue() {
  const [page, setPage] = useState(1);
  const [showReviewed, setShowReviewed] = useState(false);
  const query = useQuery({
    ...listEmergencyCancellationRequestsOptions({ query: { ...(showReviewed ? {} : { Status: 'pending' as const }), Page: page, PageSize: 25 } }),
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
  const pendingOnPage = requests.filter((request) => (request.status ?? 'pending') === 'pending').length;

  return (
    <section className="cancellation-queue" aria-labelledby="cancellation-heading">
      <div className="cancellation-intro">
        <div>
          <p className="cancellation-eyebrow">Decision queue</p>
          <h2 id="cancellation-heading">Cancellation requests</h2>
          <p>Review the caller’s reason and the live response before deciding whether the ambulance should stand down.</p>
        </div>
        {query.data && <div className="cancellation-count" aria-label={`${pendingOnPage} awaiting review on this page`}><strong>{pendingOnPage}</strong><span>awaiting review<br />on this page</span></div>}
      </div>
      <div className="flex gap-2"><Button variant={showReviewed ? 'outline' : 'primary'} aria-pressed={!showReviewed} onPress={() => { setShowReviewed(false); setPage(1); }}>Awaiting review</Button><Button variant={showReviewed ? 'primary' : 'outline'} aria-pressed={showReviewed} onPress={() => { setShowReviewed(true); setPage(1); }}>All requests</Button></div>
      <div className="cancellation-guidance" role="note">
        <span className="cancellation-guidance-icon" aria-hidden="true">i</span>
        <p><strong>Before you decide:</strong> approving cancels the emergency call and recalls any assigned ambulance. Rejecting keeps the response active and requires an explanation.</p>
      </div>
      <QueryState
        query={query}
        emptyMessage="No cancellation requests to review. New requests will appear here automatically."
        errorContext="Could not load cancellation requests."
        skeletonRows={4}
      >
        {(data) => <div className="cancellation-list">
          {requests.length === 0 && <p className="empty">No cancellation requests on this page.</p>}
          {requests.map((request) => <CancellationCard key={request.emergency_call_id} request={request} />)}
          <PaginationControls label="Cancellation requests" page={page} totalPages={data.total_pages} onPageChange={setPage} />
        </div>}
      </QueryState>
    </section>
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
    onSuccess: () => complete('Cancellation request rejected. The emergency response continues.'),
    onError: (error) => failed(error as ApiProblem),
  });

  if (callId === '') return null;
  const status = request.status ?? 'pending';
  const priority = request.call_priority ?? 'high';
  const callStatus = request.call_status ?? 'received';
  const pending = approve.isPending || reject.isPending;
  const isPending = status === 'pending';
  const caller = request.caller_name?.trim() || 'Caller name unavailable';
  const location = request.address_label?.trim() || 'Location not recorded';

  return (
    <article className={`cancellation-card ${isPending ? 'cancellation-card--pending' : 'cancellation-card--reviewed'}`} aria-label={`Cancellation request for ${caller}`}>
      <div className="cancellation-card-head">
        <div className="cancellation-card-heading">
          <p className="cancellation-card-kicker">{isPending ? 'Needs a decision' : 'Review complete'}</p>
          <h3>{caller}</h3>
          <p className="cancellation-card-time">Requested {formatTimestamp(request.requested_at)}</p>
        </div>
        <div className="cancellation-statuses" aria-label="Request, call, and priority statuses">
          <div><span>Request</span><StatusChip tone={cancellationStatusTones[status]}>{cancellationStatusLabels[status]}</StatusChip></div>
          <div><span>Response</span><StatusChip tone={callStatusTones[callStatus]}>{callStatusLabels[callStatus]}</StatusChip></div>
          <div><span>Priority</span><StatusChip tone={priorityTones[priority]}>{priorityLabels[priority]}</StatusChip></div>
        </div>
      </div>

      <div className="cancellation-card-body">
        <div className="cancellation-reason">
          <span className="cancellation-field-label">Why the caller wants to cancel</span>
          <p>{request.reason?.trim() || 'No reason provided by the caller.'}</p>
        </div>
        <dl className="cancellation-facts">
          <div><dt>Response location</dt><dd>{location}</dd></div>
          <div><dt>Assigned ambulance</dt><dd>{request.active_ambulance_registration ?? 'None assigned'}</dd></div>
          <div><dt>{isPending ? 'Time since call' : 'Reviewed'}</dt><dd>{isPending ? elapsedMinutes(request.call_created_at) : formatTimestamp(request.reviewed_at)}</dd></div>
        </dl>
      </div>

      {isPending ? (
        <div className="cancellation-actions">
          <div className="cancellation-actions-copy"><strong>Choose what happens next</strong><span>The decision is confirmed before it takes effect.</span></div>
          <div className="cancellation-action-buttons">
            <div><Button variant="danger" isDisabled={pending} onPress={() => setApproveOpen(true)}>Approve cancellation</Button><span>End response and recall ambulance</span></div>
            <div><Button variant="outline" isDisabled={pending} onPress={() => setRejectOpen(true)}>Reject request</Button><span>Keep emergency response active</span></div>
          </div>
        </div>
      ) : (
        <div className="cancellation-review-result">
          <strong>{status === 'approved' ? 'Response cancelled' : 'Response continued'}</strong>
          <span>{status === 'approved' ? 'The cancellation was approved and any assigned ambulance was recalled.' : 'The cancellation was rejected and the emergency response stayed active.'}</span>
          {request.review_notes && <p><span className="cancellation-field-label">Review notes</span>{request.review_notes}</p>}
        </div>
      )}
      <ConfirmDialog
        isOpen={approveOpen}
        onOpenChange={setApproveOpen}
        title="Approve cancellation request?"
        description="This cancels the active dispatch and recalls the ambulance, making it available for other calls. Check that stopping this response is safe."
        confirmLabel={approve.isPending ? 'Approving…' : 'Approve and recall'}
        isPending={approve.isPending}
        onConfirm={() => approve.mutate({ path: { id: callId }, body: {} })}
      />
      <ReasonDialog
        isOpen={rejectOpen}
        onOpenChange={setRejectOpen}
        title="Reject cancellation request"
        description="The emergency response will continue. Explain why it cannot be stopped; the patient can see this review result."
        notesLabel="Review notes"
        notesPlaceholder="Explain why the request cannot be approved"
        notesRequired
        confirmLabel={reject.isPending ? 'Rejecting…' : 'Reject request'}
        isPending={reject.isPending}
        onConfirm={({ notes }) => reject.mutate({ path: { id: callId }, body: { notes } })}
      />
    </article>
  );
}
