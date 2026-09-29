import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, AlertContent, AlertDescription, AlertTitle, Button } from '@heroui/react';
import { toast } from 'sonner';
import type { DispatchProposalDetail, DispatchProposalSummary, DispatchRejectionReason } from '../../../services/api/generated';
import {
  approveDispatchProposalMutation,
  confirmDispatchProposalMutation,
  createDispatchProposalMutation,
  getDispatchProposalOptions,
  rejectDispatchProposalMutation,
} from '../../../services/api/generated/@tanstack/react-query.gen';
import { QueryError, QuerySkeleton } from '../../../components/ui/query-state';
import { ReasonDialog } from '../../../components/ui/reason-dialog';
import { StatusChip } from '../../../components/ui/status-chip';
import { isConflict, problemMessage, problemStringList } from '../../../services/api/errors';
import type { ApiProblem } from '../../../services/api/errors';
import {
  dispatchStatusLabels,
  priorityLabels,
  proposalOutcomeLabels,
  recommendationSourceLabels,
  recommendationSourceTones,
  rejectionReasonLabels,
  withdrawalReasonLabels,
} from '../domain';
import { invalidateEmergencyQueries } from '../query-invalidation';

const rejectionOptions = Object.entries(rejectionReasonLabels).map(([value, label]) => ({ value, label }));

export function CallRecommendation({ callId, latest, onSent }: {
  callId: string;
  latest: DispatchProposalSummary | null | undefined;
  onSent: () => void;
}) {
  const status = latest?.status;
  if (status === 'pending') {
    return (
      <section className="call-recommendation" aria-label="Recommendation" role="status">
        <p className="m-0 font-medium">Checking available ambulances…</p>
        <p className="m-0 text-sm text-muted">The recommendation appears here by itself.</p>
      </section>
    );
  }
  if (latest?.id && (status === 'pending_confirmation' || status === 'pending_approval')) {
    return <ReadyRecommendation key={latest.id} proposalId={latest.id} onSent={onSent} />;
  }
  return <NoRecommendation callId={callId} latest={latest} />;
}

function NoRecommendation({ callId, latest }: { callId: string; latest: DispatchProposalSummary | null | undefined }) {
  const queryClient = useQueryClient();
  const detail = useQuery({ ...getDispatchProposalOptions({ path: { id: latest?.id ?? '' } }), enabled: Boolean(latest?.id) });
  const recheck = useMutation({
    ...createDispatchProposalMutation(),
    onSuccess: () => void invalidateEmergencyQueries(queryClient),
    onError: (error) => {
      if (isConflict(error)) void invalidateEmergencyQueries(queryClient);
    },
  });

  return (
    <section className="call-recommendation call-recommendation-empty" aria-label="Recommendation" role="status">
      <div>
        <p className="m-0 font-medium">No recommendation — pick an ambulance below.</p>
        <p className="m-0 text-sm text-muted">{whyNone(latest, detail.data)}</p>
        {latest?.status === 'failed' && (detail.data?.errors ?? []).map((error) => (
          <p key={`${error.step}-${error.message}`} className="m-0 text-sm text-danger">{error.message}</p>
        ))}
      </div>
      <Button variant="outline" isDisabled={recheck.isPending} onPress={() => recheck.mutate({ body: { emergency_call_id: callId, allow_diversion: true } })}>
        {recheck.isPending ? 'Asking…' : 'Re-check'}
      </Button>
    </section>
  );
}

function whyNone(latest: DispatchProposalSummary | null | undefined, detail: DispatchProposalDetail | undefined): string {
  switch (latest?.status) {
    case 'failed':
      return latest.outcome ? proposalOutcomeLabels[latest.outcome] : 'The agent could not finish.';
    case 'rejected':
      return detail?.rejection_reason ? `Last one rejected: ${rejectionReasonLabels[detail.rejection_reason]}.` : 'The last recommendation was rejected.';
    case 'withdrawn':
      return detail?.withdrawal_reason ? `Last one withdrawn: ${withdrawalReasonLabels[detail.withdrawal_reason]}.` : 'The last recommendation was withdrawn.';
    default:
      return 'The agent has not been asked about this call.';
  }
}

function ReadyRecommendation({ proposalId, onSent }: { proposalId: string; onSent: () => void }) {
  const queryClient = useQueryClient();
  const [rejectOpen, setRejectOpen] = useState(false);
  const [inlineErrors, setInlineErrors] = useState<string[]>([]);
  const detail = useQuery(getDispatchProposalOptions({ path: { id: proposalId } }));

  function settled(message: string) {
    setInlineErrors([]);
    setRejectOpen(false);
    toast.success(message);
    void invalidateEmergencyQueries(queryClient);
  }

  async function failed(error: ApiProblem, fallback: string) {
    const checks = problemStringList(error, 'failed_checks');
    setInlineErrors(checks.length > 0 ? checks : [problemMessage(error, fallback) ?? fallback]);
    if (isConflict(error)) {
      await detail.refetch();
      await invalidateEmergencyQueries(queryClient);
    }
  }

  const confirm = useMutation({
    ...confirmDispatchProposalMutation(),
    onSuccess: () => { settled('Ambulance sent. Waiting for the crew to acknowledge.'); onSent(); },
    onError: (error) => void failed(error as ApiProblem, 'Could not send this ambulance.'),
  });
  const approve = useMutation({
    ...approveDispatchProposalMutation(),
    onSuccess: () => { settled('Diversion approved and ambulance sent.'); onSent(); },
    onError: (error) => void failed(error as ApiProblem, 'Could not approve this diversion.'),
  });
  const reject = useMutation({
    ...rejectDispatchProposalMutation(),
    onSuccess: () => settled('Recommendation rejected.'),
    onError: (error) => void failed(error as ApiProblem, 'Could not reject this recommendation.'),
  });

  if (detail.isPending) return <QuerySkeleton rows={2} />;
  if (detail.isError) return <QueryError error={detail.error} context="Could not load the recommendation." onRetry={() => void detail.refetch()} />;

  const data = detail.data;
  const isDiversion = data.status === 'pending_approval';
  const latestChecks = [...new Map((data.validation ?? []).map((check) => [check.check, check])).values()];
  const persistedFailures = latestChecks
    .filter((check) => check.passed === false && check.check !== 'source_call_has_replacement')
    .map((check) => check.detail ?? check.check ?? 'Validation failed');
  const errors = [...new Set([...inlineErrors, ...persistedFailures])];
  const busy = confirm.isPending || approve.isPending || reject.isPending;

  return (
    <section className={isDiversion ? 'call-recommendation call-recommendation-diversion' : 'call-recommendation call-recommendation-ready'} aria-label="Recommendation">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="m-0 text-sm text-muted">{isDiversion ? 'Recommended diversion' : 'Recommended'}</p>
          <p className="m-0 text-lg font-semibold">{data.proposed_ambulance_registration ?? 'Ambulance'}</p>
          <p className="m-0 text-sm text-muted">
            {data.estimated_minutes_to_scene == null ? 'Arrival time unknown' : `${data.estimated_minutes_to_scene} min to scene`}
            {' · '}Crew {crewReadiness(data)}
          </p>
        </div>
        {data.recommendation_source && <StatusChip tone={recommendationSourceTones[data.recommendation_source]}>{recommendationSourceLabels[data.recommendation_source]}</StatusChip>}
      </div>
      {data.rationale && <p className="m-0">{data.rationale}</p>}
      {data.recommendation_note && <p className="m-0 text-sm text-muted">{data.recommendation_note}</p>}
      {isDiversion && data.diversion_impact && <DiversionImpact detail={data} />}
      {errors.length > 0 && <InlineErrors errors={errors} />}
      <div className="flex flex-wrap gap-2">
        {isDiversion
          ? <Button isDisabled={busy} onPress={() => approve.mutate({ path: { id: proposalId }, body: {} })}>{approve.isPending ? 'Approving…' : 'Approve diversion'}</Button>
          : <Button isDisabled={busy} onPress={() => confirm.mutate({ path: { id: proposalId } })}>{confirm.isPending ? 'Sending…' : 'Send'}</Button>}
        <Button variant="outline" isDisabled={busy} onPress={() => setRejectOpen(true)}>Reject</Button>
      </div>
      <ReasonDialog
        isOpen={rejectOpen}
        onOpenChange={setRejectOpen}
        title="Reject recommendation"
        description="Unsuitable ambulances and unsafe diversions are re-checked automatically."
        options={rejectionOptions}
        notesLabel="Review notes"
        notesPlaceholder="Explain the operational reason"
        notesRequired
        confirmLabel="Reject recommendation"
        isPending={reject.isPending}
        onConfirm={({ option, notes }) => reject.mutate({ path: { id: proposalId }, body: { reason: option as DispatchRejectionReason, notes } })}
      />
    </section>
  );
}

function DiversionImpact({ detail }: { detail: DispatchProposalDetail }) {
  const impact = detail.diversion_impact!;
  const sourcePriority = impact.source_call_priority ?? 'low';
  const sourceStatus = impact.source_dispatch_status ?? 'assigned';
  return (
    <div className="rounded-lg border border-warning p-3">
      <h4 className="font-semibold">Impact on the other call</h4>
      <dl className="mt-2 grid grid-cols-1 gap-2 text-sm sm:grid-cols-2">
        <div><dt className="text-muted">Call losing ambulance</dt><dd>{impact.source_call_address_label ?? impact.source_call_id ?? 'Unknown call'}</dd></div>
        <div><dt className="text-muted">Priority and state</dt><dd>{priorityLabels[sourcePriority]} · {dispatchStatusLabels[sourceStatus]}</dd></div>
        <div><dt className="text-muted">Already waiting</dt><dd>{impact.source_call_waiting_minutes_so_far ?? 0} min</dd></div>
        <div><dt className="text-muted">Extra wait imposed</dt><dd>{impact.source_call_additional_wait_minutes == null ? 'Unknown — no replacement assigned' : `${impact.source_call_additional_wait_minutes} min`}</dd></div>
        <div><dt className="text-muted">Replacement</dt><dd>{impact.replacement_ambulance_registration ?? 'None free'}</dd></div>
        <div><dt className="text-muted">Time saved for this call</dt><dd>{impact.minutes_saved_for_this_call == null ? 'Not estimated' : `${impact.minutes_saved_for_this_call} min`}</dd></div>
      </dl>
    </div>
  );
}

function InlineErrors({ errors }: { errors: string[] }) {
  return (
    <Alert status="danger">
      <AlertContent>
        <AlertTitle>This recommendation cannot be applied as shown.</AlertTitle>
        <AlertDescription><ul className="list-disc pl-5">{errors.map((error) => <li key={error}>{error}</li>)}</ul></AlertDescription>
      </AlertContent>
    </Alert>
  );
}

function crewReadiness(detail: DispatchProposalDetail): string {
  const current = detail.proposed_ambulance_current_crew_count;
  const required = detail.proposed_ambulance_required_crew_count;
  return current == null || required == null ? 'unknown' : `${current}/${required}`;
}
