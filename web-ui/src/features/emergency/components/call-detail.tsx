import { lazy, Suspense, useEffect, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Card, CardContent, CardTitle, Disclosure } from '@heroui/react';
import { toast } from 'sonner';
import type { AmbulanceSummaryPagedResult, CallDispatch, CallPriority, EmergencyCallDetail, ListAmbulancesError, ProblemDetails } from '../../../services/api/generated';
import {
  dispatchEmergencyCallMutation,
  updateEmergencyCallMutation,
} from '../../../services/api/generated/@tanstack/react-query.gen';
import { PaginationControls } from '../../../components/ui/pagination-controls';
import { DetailField } from '../../../components/ui/detail-field';
import { AppSelect } from '../../../components/ui/app-select';
import { QueryState } from '../../../components/ui/query-state';
import { StatusChip } from '../../../components/ui/status-chip';
import { isConflict } from '../../../services/api/errors';
import { awaitsDispatch, callStatusLabels, callStatusTones, dispatchStatusLabels, dispatchStatusTones, formatTimestamp, priorityLabels, sceneOutcomeLabels } from '../domain';
import { invalidateEmergencyQueries } from '../query-invalidation';
import { CallRecommendation } from './call-recommendation';
import { EligibleAmbulanceList } from './eligible-ambulance-list';
import type { UseQueryResult } from '@tanstack/react-query';

const liveDispatchStatuses = new Set(['assigned', 'acknowledged', 'en_route_to_scene', 'at_scene', 'transporting_to_hospital']);

const LocationPicker = lazy(() => import('./location-picker').then((module) => ({ default: module.LocationPicker })));

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

  const priorityUpdate = useMutation({
    ...updateEmergencyCallMutation(),
    onSuccess: () => {
      toast.success('Call priority updated.');
      void invalidateEmergencyQueries(queryClient);
    },
  });
  const dispatch = useMutation({
    ...dispatchEmergencyCallMutation(),
    onSuccess: () => {
      toast.success('Ambulance assigned. Waiting for crew acknowledgement.');
      void invalidateEmergencyQueries(queryClient);
      onDispatched?.();
    },
    onError: (error) => {
      if (isConflict(error)) void invalidateEmergencyQueries(queryClient);
    },
    onSettled: () => setDispatchingId(undefined),
  });

  return (
    <QueryState query={query} errorContext="Could not load this call.">
      {(call) => {
        const status = call.status ?? 'received';
        const canDispatch = awaitsDispatch({ status, active_dispatch_id: (call.dispatches ?? []).find((item) => liveDispatchStatuses.has(item.status ?? ''))?.id });
        const recommendationReady = call.latest_proposal?.status === 'pending_confirmation' || call.latest_proposal?.status === 'pending_approval';
        const busy = dispatch.isPending || priorityUpdate.isPending;
        return (
          <div className="flex flex-col gap-5">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h2 className="text-xl font-semibold">{call.caller_name ?? 'Caller details unavailable'}</h2>
                <p className="text-sm text-muted">{call.caller_phone ?? 'No phone number recorded'}</p>
              </div>
              <StatusChip tone={callStatusTones[status]}>{callStatusLabels[status]}</StatusChip>
            </div>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <DetailField label="Scene">{call.address_label ?? coordinateLabel(call)}</DetailField>
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
            {status !== 'completed' && status !== 'cancelled' && <div className="flex flex-wrap items-end gap-3">
              <PriorityControl
                current={call.priority ?? 'high'}
                pending={busy}
                onSave={(priority) => priorityUpdate.mutate({ path: { id: callId }, body: { priority } })}
              />
            </div>}
            {canDispatch && <CallRecommendation callId={callId} latest={call.latest_proposal} onSent={() => onDispatched?.()} />}
            {!canDispatch && <p className="workflow-notice">{status === 'completed' || status === 'cancelled' ? 'This call is closed. Its response history is shown below.' : 'A response is already active. Follow its progress below; another ambulance cannot be assigned to this call.'}</p>}
            <DispatchHistory call={call} />
            {canDispatch && <Disclosure key={`${callId}-${recommendationReady}`} defaultExpanded={!recommendationReady}>
              <Disclosure.Heading>
                <Disclosure.Trigger className="font-semibold">
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
          </div>
        );
      }}
    </QueryState>
  );
}

function PriorityControl({ current, pending, onSave }: { current: CallPriority; pending: boolean; onSave: (priority: CallPriority) => void }) {
  const [priority, setPriority] = useState(current);
  useEffect(() => setPriority(current), [current]);
  return (
    <div className="flex flex-col gap-1 text-sm">
      <span className="flex gap-2">
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
      </span>
    </div>
  );
}

function DispatchHistory({ call }: { call: EmergencyCallDetail }) {
  const dispatches = call.dispatches ?? [];
  if (dispatches.length === 0) return <p className="muted">No dispatch has been recorded for this call.</p>;
  return (
    <section className="flex flex-col gap-2">
      <h3 className="font-semibold">Dispatch history</h3>
      {dispatches.map((dispatch, index) => {
        const status = dispatch.status ?? 'assigned';
        return (
          <Card key={dispatch.id ?? index}>
            <CardContent>
              <div className="flex items-center justify-between gap-3">
                <CardTitle>{dispatch.ambulance_registration ?? 'Ambulance'}</CardTitle>
                <StatusChip tone={dispatchStatusTones[status]}>{dispatchStatusLabels[status]}</StatusChip>
              </div>
              <p className="text-sm text-muted">Dispatched {formatTimestamp(dispatch.dispatched_at)} · crew {dispatch.crew_count ?? 0}</p>
              {status === 'assigned' && (
                <p className={dispatch.acknowledgement_overdue ? 'mt-2 text-sm text-danger' : 'mt-2 text-sm text-warning'} role={dispatch.acknowledgement_overdue ? 'alert' : 'status'}>
                  {dispatch.acknowledgement_overdue
                    ? 'Crew acknowledgement is overdue. Dispatcher attention required.'
                    : 'Waiting for crew acknowledgement.'}
                </p>
              )}
              <HowItEnded dispatch={dispatch} call={call} />
            </CardContent>
          </Card>
        );
      })}
    </section>
  );
}

function HowItEnded({ dispatch, call }: { dispatch: CallDispatch; call: EmergencyCallDetail }) {
  const endedAt = dispatch.completed_at ? formatTimestamp(dispatch.completed_at) : undefined;
  switch (dispatch.status) {
    case 'handed_over':
      return (
        <EndingLines lines={[
          ['Handed over', endedAt],
          ['Condition on arrival', dispatch.patient_condition ?? 'Not recorded'],
          ['Notes', dispatch.handover_notes ?? 'No notes were written at handover.'],
        ]} />
      );
    case 'ended_at_scene':
      return (
        <EndingLines lines={[
          ['Finished at the scene', call.scene_outcome ? sceneOutcomeLabels[call.scene_outcome] : 'No reason recorded'],
          ['Ended', endedAt],
          ['Crew notes', call.scene_outcome_notes ?? undefined],
        ]} />
      );
    case 'declined':
      return <EndingLines lines={[['Declined by the crew', dispatch.declined_reason ?? 'No reason recorded']]} />;
    case 'cancelled':
      return <EndingLines lines={[['Cancelled', dispatch.cancellation_reason ?? 'No reason recorded']]} />;
    case 'reassigned':
      return <EndingLines lines={[['Given to another ambulance', dispatch.reassignment_reason ?? 'No reason recorded']]} />;
    default:
      return null;
  }
}

function EndingLines({ lines }: { lines: [label: string, value: string | undefined][] }) {
  const shown = lines.filter((line): line is [string, string] => Boolean(line[1]));
  return (
    <dl className="mt-2 flex flex-col gap-0.5 text-sm">
      {shown.map(([label, value]) => (
        <div key={label} className="flex flex-wrap gap-x-2">
          <dt className="text-muted">{label}:</dt>
          <dd>{value}</dd>
        </div>
      ))}
    </dl>
  );
}

function coordinateLabel(call: EmergencyCallDetail): string {
  return call.latitude == null || call.longitude == null ? 'Unknown' : `${call.latitude.toFixed(5)}, ${call.longitude.toFixed(5)}`;
}
