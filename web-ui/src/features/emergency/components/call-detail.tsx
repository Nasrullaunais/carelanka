import { lazy, Suspense, useEffect, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Card, CardContent, CardTitle } from '@heroui/react';
import { toast } from 'sonner';
import type { AmbulanceSummaryPagedResult, CallPriority, EmergencyCallDetail, ListAmbulancesError, ProblemDetails } from '../../../services/api/generated';
import {
  createDispatchProposalMutation,
  dispatchEmergencyCallMutation,
  updateEmergencyCallMutation,
} from '../../../services/api/generated/@tanstack/react-query.gen';
import { DetailField } from '../../../components/ui/detail-field';
import { AppSelect } from '../../../components/ui/app-select';
import { QueryState } from '../../../components/ui/query-state';
import { StatusChip } from '../../../components/ui/status-chip';
import { isConflict } from '../../../services/api/errors';
import { callStatusLabels, callStatusTones, dispatchStatusLabels, dispatchStatusTones, formatTimestamp, priorityLabels } from '../domain';
import { invalidateEmergencyQueries } from '../query-invalidation';
import { EligibleAmbulanceList } from './eligible-ambulance-list';
import type { UseQueryResult } from '@tanstack/react-query';

const LocationPicker = lazy(() => import('./location-picker').then((module) => ({ default: module.LocationPicker })));

export function CallDetail({ callId, query, ambulances }: {
  callId: string;
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
    },
    onError: (error) => {
      if (isConflict(error)) {
        toast.error('Another dispatcher changed this call or ambulance first. The boards have been refreshed.');
        void invalidateEmergencyQueries(queryClient);
      }
    },
    onSettled: () => setDispatchingId(undefined),
  });
  const proposal = useMutation({
    ...createDispatchProposalMutation(),
    onSuccess: () => {
      toast.success('Dispatch recommendation requested.');
      void invalidateEmergencyQueries(queryClient);
    },
    onError: (error) => {
      if (isConflict(error)) {
        toast.error('This call already has an active recommendation or dispatch.');
        void invalidateEmergencyQueries(queryClient);
      }
    },
  });

  return (
    <QueryState query={query} errorContext="Could not load this call.">
      {(call) => {
        const status = call.status ?? 'received';
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
            <div className="flex flex-wrap items-end gap-3">
              <PriorityControl
                current={call.priority ?? 'high'}
                pending={priorityUpdate.isPending}
                onSave={(priority) => priorityUpdate.mutate({ path: { id: callId }, body: { priority } })}
              />
              <Button
                variant="outline"
                isDisabled={proposal.isPending || Boolean(call.open_proposal_id)}
                onPress={() => proposal.mutate({ body: { emergency_call_id: callId, allow_diversion: true } })}
              >
                {call.open_proposal_id ? 'Recommendation pending' : proposal.isPending ? 'Requesting…' : 'Ask the agent'}
              </Button>
            </div>
            <DispatchHistory dispatches={call.dispatches ?? []} />
            <section className="flex flex-col gap-2">
              <h3 className="font-semibold">Ambulance choices</h3>
              <EligibleAmbulanceList
                ambulances={ambulances.data?.items}
                isLoading={ambulances.isPending}
                error={ambulances.error}
                dispatchingId={dispatchingId}
                onRetry={() => void ambulances.refetch()}
                onDispatch={(ambulanceId) => {
                  setDispatchingId(ambulanceId);
                  dispatch.mutate({ path: { id: callId }, body: { ambulance_id: ambulanceId } });
                }}
              />
            </section>
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

function DispatchHistory({ dispatches }: { dispatches: NonNullable<EmergencyCallDetail['dispatches']> }) {
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
            </CardContent>
          </Card>
        );
      })}
    </section>
  );
}

function coordinateLabel(call: EmergencyCallDetail): string {
  return call.latitude == null || call.longitude == null ? 'Unknown' : `${call.latitude.toFixed(5)}, ${call.longitude.toFixed(5)}`;
}
