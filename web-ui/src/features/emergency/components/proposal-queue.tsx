import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, AlertContent, AlertDescription, AlertTitle, Button, Card, CardContent, CardTitle } from '@heroui/react';
import { toast } from 'sonner';
import type { DispatchProposalDetail, DispatchProposalSummary, DispatchRejectionReason } from '../../../services/api/generated';
import { approveDispatchProposalMutation, confirmDispatchProposalMutation, getDispatchProposalOptions, rejectDispatchProposalMutation } from '../../../services/api/generated/@tanstack/react-query.gen';
import { QueryError, QuerySkeleton } from '../../../components/ui/query-state';
import { ReasonDialog } from '../../../components/ui/reason-dialog';
import { StatusChip } from '../../../components/ui/status-chip';
import { isConflict, problemMessage, problemStringList } from '../../../services/api/errors';
import type { ApiProblem } from '../../../services/api/errors';
import { dispatchStatusLabels, formatTimestamp, priorityLabels, priorityTones, proposalOutcomeLabels, proposalStatusLabels, proposalStatusTones, rejectionReasonLabels } from '../domain';
import { useActionableProposals } from '../hooks/use-actionable-proposals';
import { invalidateEmergencyQueries } from '../query-invalidation';

const rejectionOptions = Object.entries(rejectionReasonLabels).map(([value, label]) => ({ value, label }));

export function ProposalQueue({ onOpenCall }: { onOpenCall?: (id: string) => void }) {
  const proposals = useActionableProposals();
  if (proposals.isPending) return <QuerySkeleton rows={4} />;
  if (proposals.error != null) return <QueryError error={proposals.error} context="Could not load dispatch proposals." onRetry={() => void proposals.refetch()} />;
  if (proposals.items.length === 0) return <section className="workflow-empty"><h2>No recommendations awaiting review</h2><p className="muted">Open a call and choose Ask the agent to check available ambulances. New recommendations appear here automatically.</p></section>;
  return <section className="flex flex-col gap-4" aria-labelledby="proposal-heading"><div><h2 id="proposal-heading">Dispatch recommendations</h2><p className="muted">Review the agent’s reasoning and live crew readiness before sending a response.</p></div>{proposals.items.map((proposal) => proposal.id && <ProposalCard key={proposal.id} proposal={proposal} onOpenCall={onOpenCall} />)}{proposals.hasNextPage && <Button variant="outline" isDisabled={proposals.isFetchingNextPage} onPress={() => void proposals.fetchNextPage()}>{proposals.isFetchingNextPage ? 'Loading…' : 'Load more recommendations'}</Button>}</section>;
}

function ProposalCard({ proposal, onOpenCall }: { proposal: DispatchProposalSummary; onOpenCall?: (id: string) => void }) {
  const proposalId = proposal.id ?? '';
  const queryClient = useQueryClient();
  const [rejectOpen, setRejectOpen] = useState(false);
  const [inlineErrors, setInlineErrors] = useState<string[]>([]);
  const detail = useQuery({ ...getDispatchProposalOptions({ path: { id: proposalId } }), enabled: proposalId !== '', refetchInterval: 5_000 });

  function complete(message: string) {
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

  const confirm = useMutation({ ...confirmDispatchProposalMutation(), onSuccess: () => complete('Recommendation sent to the crew.'), onError: (error) => void failed(error as ApiProblem, 'Could not confirm this recommendation.') });
  const approve = useMutation({ ...approveDispatchProposalMutation(), onSuccess: () => complete('Diversion approved and dispatch updated.'), onError: (error) => void failed(error as ApiProblem, 'Could not approve this diversion.') });
  const reject = useMutation({ ...rejectDispatchProposalMutation(), onSuccess: () => complete('Recommendation rejected.'), onError: (error) => void failed(error as ApiProblem, 'Could not reject this recommendation.') });

  if (detail.isPending) return <QuerySkeleton rows={3} />;
  if (detail.isError) return <QueryError error={detail.error} context="Could not load proposal detail." onRetry={() => void detail.refetch()} />;
  if (!detail.data) return null;

  const data = detail.data;
  const status = data.status ?? 'pending';
  const priority = data.call_priority ?? 'high';
  const latestChecks = [...new Map((data.validation ?? []).map((check) => [check.check, check])).values()];
  const persistedFailures = latestChecks.filter((check) => check.passed === false && check.check !== 'source_call_has_replacement').map((check) => check.detail ?? check.check ?? 'Validation failed');
  const errors = [...new Set([...inlineErrors, ...persistedFailures, ...(data.errors ?? []).map((error) => error.message ?? 'Proposal processing failed')])];
  const pending = confirm.isPending || approve.isPending || reject.isPending;

  return (
    <Card>
      <CardContent className="flex flex-col gap-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div><CardTitle>{data.is_diversion ? 'Diversion review' : 'Dispatch recommendation'}</CardTitle><p className="text-sm text-muted">Created {formatTimestamp(data.created_at)}</p></div>
          <div className="flex gap-2"><StatusChip tone={priorityTones[priority]}>{priorityLabels[priority]}</StatusChip><StatusChip tone={proposalStatusTones[status]}>{proposalStatusLabels[status]}</StatusChip></div>
        </div>
        <div>
          <p className="font-medium">{data.proposed_ambulance_registration ?? 'No ambulance proposed'}</p>
          <p className="text-sm text-muted">{data.estimated_minutes_to_scene == null ? 'ETA unavailable' : `${data.estimated_minutes_to_scene} min to scene`}</p>
          {data.proposed_ambulance_registration && <p className="text-sm text-muted">Crew readiness: {crewReadiness(data)}</p>}
          <p className="mt-2">{data.rationale ?? (data.outcome ? proposalOutcomeLabels[data.outcome] : status === 'pending' ? 'The agent is checking available crews and routes. This recommendation updates automatically.' : 'No reasoning is available.')}</p>
        </div>
        {data.emergency_call_id && onOpenCall && <Button variant="outline" onPress={() => onOpenCall(data.emergency_call_id!)}>{status === 'failed' ? 'Return to call and retry or dispatch manually' : 'View emergency call'}</Button>}
        {data.is_diversion && data.diversion_impact && <DiversionImpact detail={data} />}
        {errors.length > 0 && <InlineErrors errors={errors} />}
        {(status === 'pending_confirmation' || status === 'pending_approval') && (
          <div className="flex flex-wrap gap-2">
            {status === 'pending_confirmation' && <Button isDisabled={pending} onPress={() => confirm.mutate({ path: { id: proposalId } })}>{confirm.isPending ? 'Sending…' : 'Send'}</Button>}
            {status === 'pending_approval' && <Button isDisabled={pending} onPress={() => approve.mutate({ path: { id: proposalId }, body: {} })}>{approve.isPending ? 'Approving…' : 'Approve diversion'}</Button>}
            <Button variant="outline" isDisabled={pending} onPress={() => setRejectOpen(true)}>Reject</Button>
          </div>
        )}
      </CardContent>
      <ReasonDialog
        isOpen={rejectOpen}
        onOpenChange={setRejectOpen}
        title="Reject dispatch recommendation"
        description="Record why this recommendation should not be used."
        options={rejectionOptions}
        notesLabel="Review notes"
        notesPlaceholder="Explain the operational reason"
        notesRequired
        confirmLabel="Reject recommendation"
        isPending={reject.isPending}
        onConfirm={({ option, notes }) => reject.mutate({ path: { id: proposalId }, body: { reason: option as DispatchRejectionReason, notes } })}
      />
    </Card>
  );
}

function DiversionImpact({ detail }: { detail: DispatchProposalDetail }) {
  const impact = detail.diversion_impact!;
  const sourcePriority = impact.source_call_priority ?? 'low';
  const sourceStatus = impact.source_dispatch_status ?? 'assigned';
  return (
    <div className="rounded-lg border border-warning p-3">
      <h4 className="font-semibold">Impact on the current call</h4>
      <dl className="mt-2 grid grid-cols-1 gap-2 text-sm sm:grid-cols-2">
        <div><dt className="text-muted">Call losing ambulance</dt><dd>{impact.source_call_address_label ?? impact.source_call_id ?? 'Unknown call'}</dd></div>
        <div><dt className="text-muted">Priority and state</dt><dd>{priorityLabels[sourcePriority]} · {dispatchStatusLabels[sourceStatus]}</dd></div>
        <div><dt className="text-muted">Already waiting</dt><dd>{impact.source_call_waiting_minutes_so_far ?? 0} min</dd></div>
        <div><dt className="text-muted">Extra wait imposed</dt><dd>{impact.source_call_additional_wait_minutes == null ? 'Unknown — no replacement assigned' : `${impact.source_call_additional_wait_minutes} min`}</dd></div>
        <div><dt className="text-muted">Replacement</dt><dd>{impact.replacement_ambulance_registration ?? 'None free'}</dd></div>
        <div><dt className="text-muted">Critical-call time saved</dt><dd>{impact.minutes_saved_for_this_call == null ? 'Not estimated' : `${impact.minutes_saved_for_this_call} min`}</dd></div>
      </dl>
    </div>
  );
}

function InlineErrors({ errors }: { errors: string[] }) {
  return <Alert status="danger"><AlertContent><AlertTitle>This recommendation cannot be applied as shown.</AlertTitle><AlertDescription><ul className="list-disc pl-5">{errors.map((error) => <li key={error}>{error}</li>)}</ul></AlertDescription></AlertContent></Alert>;
}

function crewReadiness(detail: DispatchProposalDetail): string {
  const current = detail.proposed_ambulance_current_crew_count;
  const required = detail.proposed_ambulance_required_crew_count;
  return current == null || required == null ? 'Not included in the proposal response' : `${current}/${required} assigned`;
}
