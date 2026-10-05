import { lazy, Suspense, useEffect, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Card, CardContent, CardTitle, Disclosure } from '@heroui/react';
import { toast } from 'sonner';
import type {
  AmbulanceSummary,
  AmbulanceSummaryPagedResult,
  CallPriority,
  DispatchDetail,
  EmergencyCallDetail,
  EmergencyCallOutcome,
  ListAmbulancesError,
  ProblemDetails,
} from '../../../services/api/generated';
import {
  cancelDispatchMutation,
  cancelEmergencyCallMutation,
  dispatchEmergencyCallMutation,
  reassignDispatchMutation,
  updateEmergencyCallMutation,
} from '../../../services/api/generated/@tanstack/react-query.gen';
import { PaginationControls } from '../../../components/ui/pagination-controls';
import { DetailField } from '../../../components/ui/detail-field';
import { AppSelect } from '../../../components/ui/app-select';
import { QueryState } from '../../../components/ui/query-state';
import { ReasonDialog } from '../../../components/ui/reason-dialog';
import { StatusChip } from '../../../components/ui/status-chip';
import { isConflict } from '../../../services/api/errors';
import {
  awaitsDispatch,
  callCloseOutcomes,
  callOutcomeLabels,
  callStatusLabels,
  callStatusTones,
  dispatchStatusLabels,
  dispatchStatusTones,
  formatDriveMinutes,
  formatKilometres,
  formatTimestamp,
  isClosedCall,
  isLiveDispatch,
  isPrePickup,
  priorityLabels,
} from '../domain';
import { invalidateEmergencyQueries } from '../query-invalidation';
import { CallRecommendation } from './call-recommendation';
import { EditCallDialog } from './edit-call-dialog';
import { EligibleAmbulanceList } from './eligible-ambulance-list';
import type { UseQueryResult } from '@tanstack/react-query';

const LocationPicker = lazy(() => import('./location-picker').then((module) => ({ default: module.LocationPicker })));

type RunAction = { kind: 'cancel' | 'reassign'; dispatch: DispatchDetail };

export function CallDetail({ callId, query, ambulances, onDispatched, ambulancePage = 1, onAmbulancePageChange }: {
  ambulancePage?: number;
  onAmbulancePageChange?: (page: number) => void;
  callId: string;
  onDispatched?: () => void;
  query: UseQueryResult<EmergencyCallDetail, ProblemDetails>;
  ambulances: UseQueryResult<AmbulanceSummaryPagedResult, ListAmbulancesError>;
}) {
  const queryClient = useQueryClient();
  const [dispatchingId, setDispatchingId] = useState<string>();
  const [editOpen, setEditOpen] = useState(false);
  const [closeOpen, setCloseOpen] = useState(false);
  const [runAction, setRunAction] = useState<RunAction>();
  const refresh = () => invalidateEmergencyQueries(queryClient);
  const refreshOnConflict = (error: ProblemDetails) => { if (isConflict(error)) void refresh(); };

  const update = useMutation({
    ...updateEmergencyCallMutation(),
    onSuccess: (_, variables) => {
      setEditOpen(false);
      toast.success(variables.body.priority ? 'Call priority updated.' : 'Call details saved.');
      void refresh();
    },
  });
  const dispatch = useMutation({
    ...dispatchEmergencyCallMutation(),
    onSuccess: () => {
      toast.success('Ambulance assigned. Waiting for crew acknowledgement.');
      void refresh();
      onDispatched?.();
    },
    onError: refreshOnConflict,
    onSettled: () => setDispatchingId(undefined),
  });
  const closeCall = useMutation({
    ...cancelEmergencyCallMutation(),
    onSuccess: () => {
      setCloseOpen(false);
      toast.success('Call closed.');
      void refresh();
    },
    onError: refreshOnConflict,
  });
  const cancelRun = useMutation({
    ...cancelDispatchMutation(),
    onSuccess: () => {
      setRunAction(undefined);
      toast.success('Ambulance called off. The crew has been told to stop, and a new recommendation is being prepared.');
      void refresh();
    },
    onError: refreshOnConflict,
  });
  const reassignRun = useMutation({
    ...reassignDispatchMutation(),
    onSuccess: (replacement) => {
      setRunAction(undefined);
      toast.success(`${replacement.ambulance_registration ?? 'The new ambulance'} is on this call now. The first crew has been told to stop.`);
      void refresh();
    },
    onError: refreshOnConflict,
  });

  return (
    <QueryState query={query} errorContext="Could not load this call.">
      {(call) => {
        const status = call.status ?? 'received';
        const closed = isClosedCall(status);
        const liveRun = (call.dispatches ?? []).find((item) => isLiveDispatch(item.status));
        const canDispatch = awaitsDispatch({ status, active_dispatch_id: liveRun?.id });
        const canClose = !closed && (!liveRun || isPrePickup(liveRun.status));
        const recommendationReady = call.latest_proposal?.status === 'pending_confirmation' || call.latest_proposal?.status === 'pending_approval';
        const busy = dispatch.isPending || update.isPending || closeCall.isPending;
        return (
          <div className="flex flex-col gap-5">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h2 className="text-xl font-semibold">{call.caller_name ?? 'Caller details unavailable'}</h2>
                <p className="text-sm text-muted">{call.caller_phone ?? 'No phone number recorded'}</p>
                {call.patient_name && call.patient_name !== call.caller_name && <p className="text-sm">Patient: <strong>{call.patient_name}</strong></p>}
              </div>
              <StatusChip tone={callStatusTones[status]}>{callStatusLabels[status]}</StatusChip>
            </div>
            {closed && <ClosedCallSummary call={call} />}
            <DetailField label="Scene">{call.address_label ?? coordinateLabel(call)}</DetailField>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
              <DetailField label="Reported">{formatTimestamp(call.created_at)}</DetailField>
              <DetailField label="Location accuracy">{call.location_accuracy_metres == null ? 'Unknown' : `${call.location_accuracy_metres} m`}</DetailField>
              <DetailField label="Patient is caller">{call.patient_is_caller ? 'Yes' : 'No'}</DetailField>
            </div>
            <DetailField label="Emergency details">{call.details ?? 'No caller report was recorded.'}</DetailField>
            {call.latitude != null && call.longitude != null && (
              <section className="flex flex-col gap-2">
                <div className="flex items-center justify-between gap-3">
                  <h3 className="font-semibold">Scene location</h3>
                  <a className="text-sm text-accent underline" href={`https://www.google.com/maps?q=${call.latitude},${call.longitude}`} target="_blank" rel="noreferrer">Open in Google Maps</a>
                </div>
                <Suspense fallback={<div className="h-64 animate-pulse rounded-xl bg-default-100" />}>
                  <LocationPicker value={{ latitude: call.latitude, longitude: call.longitude }} onChange={() => undefined} readOnly />
                </Suspense>
              </section>
            )}
            {!closed && <div className="flex flex-wrap items-end gap-3">
              <PriorityControl
                current={call.priority ?? 'high'}
                pending={busy}
                onSave={(priority) => update.mutate({ path: { id: callId }, body: { priority } })}
              />
              <div className="ml-auto flex gap-2">
                <Button size="sm" variant="outline" isDisabled={busy} onPress={() => setEditOpen(true)}>Edit call</Button>
                {canClose && <Button size="sm" variant="danger" isDisabled={busy} onPress={() => setCloseOpen(true)}>Close call</Button>}
              </div>
            </div>}
            {canDispatch && <CallRecommendation
              callId={callId}
              latest={call.latest_proposal}
              ambulanceFree={ambulances.data?.items?.some((ambulance) => ambulance.is_eligible) ?? false}
              onSent={() => onDispatched?.()}
            />}
            {!canDispatch && !closed && <p className="workflow-notice">{liveRun && isPrePickup(liveRun.status)
              ? 'An ambulance is on its way. You can call it off or send a different one until the crew reaches the patient.'
              : 'The crew is with the patient. The run can only be ended by the crew now.'}</p>}
            {closed && <p className="workflow-notice">This call is closed. Its response history is shown below.</p>}
            <DispatchHistory
              dispatches={call.dispatches ?? []}
              onCancel={(item) => setRunAction({ kind: 'cancel', dispatch: item })}
              onReassign={(item) => setRunAction({ kind: 'reassign', dispatch: item })}
            />
            {canDispatch && <Disclosure key={`${callId}-${recommendationReady}`} defaultExpanded={!recommendationReady}>
              <Disclosure.Heading>
                <Disclosure.Trigger className="call-disclosure-trigger">
                  {recommendationReady ? 'Other ambulances' : 'Ambulance choices'}
                  <Disclosure.Indicator />
                </Disclosure.Trigger>
              </Disclosure.Heading>
              <Disclosure.Content>
                <Disclosure.Body className="flex flex-col gap-2">
                  <EligibleAmbulanceList
                    ambulances={ambulances.data?.items}
                    isLoading={ambulances.isPending}
                    error={ambulances.error}
                    dispatchingId={dispatchingId}
                    isDisabled={busy}
                    onRetry={() => void ambulances.refetch()}
                    onDispatch={(ambulanceId) => {
                      setDispatchingId(ambulanceId);
                      dispatch.mutate({ path: { id: callId }, body: { ambulance_id: ambulanceId } });
                    }}
                  />
                  {ambulances.data && onAmbulancePageChange && <PaginationControls label="Ambulance choices" page={ambulancePage} totalPages={ambulances.data.total_pages} onPageChange={onAmbulancePageChange} />}
                </Disclosure.Body>
              </Disclosure.Content>
            </Disclosure>}
            {editOpen && <EditCallDialog
              call={call}
              isOpen
              isPending={update.isPending}
              onOpenChange={setEditOpen}
              onSave={(body) => update.mutate({ path: { id: callId }, body })}
            />}
            <ReasonDialog
              isOpen={closeOpen}
              onOpenChange={setCloseOpen}
              title="Close this call?"
              description={liveRun
                ? `${liveRun.ambulance_registration ?? 'The ambulance'} will be called off and its crew told to stop. The caller is told the request is closed.`
                : 'No ambulance will be sent. The caller is told the request is closed.'}
              options={callCloseOutcomes.map((value) => ({ value, label: callOutcomeLabels[value] }))}
              optionLabel="Why is it closing?"
              notesLabel="Notes (optional)"
              notesPlaceholder="For example: the same incident as the call from 10:42"
              confirmLabel={closeCall.isPending ? 'Closing…' : 'Close call'}
              isPending={closeCall.isPending}
              onConfirm={({ option, notes }) => option && closeCall.mutate({
                path: { id: callId },
                body: { outcome: option as EmergencyCallOutcome, notes: notes?.trim() },
              })}
            />
            <ReasonDialog
              isOpen={runAction?.kind === 'cancel'}
              onOpenChange={(open) => !open && setRunAction(undefined)}
              title={`Call off ${runAction?.dispatch.ambulance_registration ?? 'this ambulance'}?`}
              description="The crew is told to stop and the ambulance becomes free. The call goes back to waiting, and a new recommendation is prepared without this ambulance."
              notesLabel="Reason"
              notesPlaceholder="For example: crew not answering"
              notesRequired
              confirmLabel={cancelRun.isPending ? 'Calling off…' : 'Call off ambulance'}
              isPending={cancelRun.isPending}
              onConfirm={({ notes }) => runAction?.dispatch.id && notes && cancelRun.mutate({ path: { id: runAction.dispatch.id }, body: { reason: notes.trim() } })}
            />
            <ReasonDialog
              isOpen={runAction?.kind === 'reassign'}
              onOpenChange={(open) => !open && setRunAction(undefined)}
              title={`Replace ${runAction?.dispatch.ambulance_registration ?? 'this ambulance'}`}
              description={replacementOptions(ambulances.data?.items, runAction?.dispatch).length === 0
                ? 'No other ambulance can be sent right now. Call this one off instead, or wait for one to become free.'
                : 'The first crew is told to stop and the new crew gets the run straight away.'}
              options={replacementOptions(ambulances.data?.items, runAction?.dispatch)}
              optionLabel="Send instead"
              notesLabel="Reason"
              notesPlaceholder="For example: a closer ambulance became free"
              notesRequired
              confirmLabel={reassignRun.isPending ? 'Sending…' : 'Send this ambulance'}
              isPending={reassignRun.isPending}
              onConfirm={({ option, notes }) => runAction?.dispatch.id && option && notes && reassignRun.mutate({
                path: { id: runAction.dispatch.id },
                body: { replacement_ambulance_id: option, reason: notes.trim() },
              })}
            />
          </div>
        );
      }}
    </QueryState>
  );
}

function ClosedCallSummary({ call }: { call: EmergencyCallDetail }) {
  return (
    <div className="grid grid-cols-1 gap-3 rounded-xl border border-default-200 p-3 sm:grid-cols-3">
      <DetailField label="How it ended">{call.outcome ? callOutcomeLabels[call.outcome] : 'Not recorded'}</DetailField>
      <DetailField label="Closed">{formatTimestamp(call.closed_at)}</DetailField>
      <DetailField label="Taken to hospital">{call.transported == null ? 'Unknown' : call.transported ? 'Yes' : 'No'}</DetailField>
      {call.outcome_notes && <div className="sm:col-span-3"><DetailField label="Notes">{call.outcome_notes}</DetailField></div>}
    </div>
  );
}

function PriorityControl({ current, pending, onSave }: { current: CallPriority; pending: boolean; onSave: (priority: CallPriority) => void }) {
  const [priority, setPriority] = useState(current);
  useEffect(() => setPriority(current), [current]);
  return (
    <div className="flex items-end gap-2 text-sm">
      <AppSelect
        className="min-w-40"
        label="Priority"
        value={priority}
        onValueChange={(value) => setPriority(value as CallPriority)}
        options={Object.entries(priorityLabels).map(([value, label]) => ({ value, label }))}
      />
      <Button size="sm" isDisabled={pending || priority === current} onPress={() => onSave(priority)}>
        {pending ? 'Saving…' : 'Update'}
      </Button>
    </div>
  );
}

function DispatchHistory({ dispatches, onCancel, onReassign }: {
  dispatches: DispatchDetail[];
  onCancel: (dispatch: DispatchDetail) => void;
  onReassign: (dispatch: DispatchDetail) => void;
}) {
  if (dispatches.length === 0) return <p className="muted">No dispatch has been recorded for this call.</p>;
  return (
    <section className="flex flex-col gap-2">
      <h3 className="font-semibold">Dispatch history</h3>
      {dispatches.map((dispatch, index) => {
        const status = dispatch.status ?? 'assigned';
        const notes = runNotes(dispatch);
        return (
          <Card key={dispatch.id ?? index}>
            <CardContent className="flex flex-col gap-2">
              <div className="flex items-center justify-between gap-3">
                <CardTitle>{dispatch.ambulance_registration ?? 'Ambulance'}</CardTitle>
                <StatusChip tone={dispatchStatusTones[status]}>{dispatchStatusLabels[status]}</StatusChip>
              </div>
              <p className="text-sm text-muted">
                Dispatched {formatTimestamp(dispatch.dispatched_at)} · crew {dispatch.crew_count ?? 0}
                {dispatch.completed_at && ` · ended ${formatTimestamp(dispatch.completed_at)}`}
              </p>
              {status === 'assigned' && (
                <p className={dispatch.acknowledgement_overdue ? 'text-sm text-danger' : 'text-sm text-warning'} role={dispatch.acknowledgement_overdue ? 'alert' : 'status'}>
                  {dispatch.acknowledgement_overdue
                    ? 'The crew has not accepted yet. Ring them, or send a different ambulance.'
                    : 'Waiting for the crew to accept.'}
                </p>
              )}
              {notes.length > 0 && (
                <dl className="grid grid-cols-1 gap-2 text-sm sm:grid-cols-2">
                  {notes.map(([label, text]) => <div key={label}><dt className="text-muted">{label}</dt><dd className="whitespace-pre-wrap">{text}</dd></div>)}
                </dl>
              )}
              {isPrePickup(status) && (
                <div className="flex flex-wrap gap-2">
                  <Button size="sm" variant="outline" onPress={() => onReassign(dispatch)}>Send a different ambulance</Button>
                  <Button size="sm" variant="danger" onPress={() => onCancel(dispatch)}>Call off this ambulance</Button>
                </div>
              )}
            </CardContent>
          </Card>
        );
      })}
    </section>
  );
}

function runNotes(dispatch: DispatchDetail): Array<[string, string]> {
  const notes: Array<[string, string | null | undefined]> = [
    ['Why the crew declined', dispatch.declined_reason],
    ['Why it was called off', dispatch.cancellation_reason],
    ['Why it was replaced', dispatch.reassignment_reason],
    ['Patient condition at handover', dispatch.patient_condition],
    ['Handover notes', dispatch.handover_notes],
  ];
  return notes.filter((note): note is [string, string] => Boolean(note[1]?.trim()));
}

function replacementOptions(ambulances: AmbulanceSummary[] | undefined, current: DispatchDetail | undefined) {
  return (ambulances ?? [])
    .filter((ambulance) => ambulance.is_eligible && ambulance.id !== current?.ambulance_id)
    .map((ambulance) => ({
      value: ambulance.id,
      label: [
        ambulance.registration_number,
        ambulance.distance_km != null && formatKilometres(ambulance.distance_km),
        ambulance.drive_minutes != null && formatDriveMinutes(ambulance.drive_minutes),
      ].filter(Boolean).join(' · '),
    }));
}

function coordinateLabel(call: EmergencyCallDetail): string {
  return call.latitude == null || call.longitude == null ? 'Unknown' : `${call.latitude.toFixed(5)}, ${call.longitude.toFixed(5)}`;
}
