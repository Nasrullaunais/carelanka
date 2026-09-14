import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  assignCurrentAmbulanceCrewMutation,
  dispatchEmergencyCallMutation,
  getCurrentAmbulanceCrewOptions,
  getEmergencyCallOptions,
  listAmbulancesOptions,
  listEmergencyCallsOptions,
  updateEmergencyCallMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { AmbulanceEligibilityBlockReason, CallPriority, DispatchSummary } from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { canManageEmergency } from '../types/permissions';

const PAGE_SIZE = 50;
const acknowledgementWindowMs = 30_000;

const priorityLabels: Record<CallPriority, string> = {
  critical: 'Critical',
  high: 'High',
  medium: 'Medium',
  low: 'Low',
};

const blockReasonLabels: Record<AmbulanceEligibilityBlockReason, string> = {
  inactive: 'Inactive',
  out_of_service: 'Out of service',
  insufficient_crew: 'Not enough current crew',
  active_dispatch: 'Already on a live dispatch',
  missing_location: 'No current location',
  stale_location: 'Location is stale',
};

export function EmergencyPage() {
  const session = useSession();
  const role = session?.principal.role;
  const queryClient = useQueryClient();
  const [selectedCallId, setSelectedCallId] = useState<string>();
  const [selectedAmbulanceId, setSelectedAmbulanceId] = useState<string>();
  const [crewStaffId, setCrewStaffId] = useState('');
  const [notice, setNotice] = useState<string>();

  const calls = useQuery(listEmergencyCallsOptions({ query: { page: 1, pageSize: PAGE_SIZE } }));
  const selectedCall = useQuery({
    ...getEmergencyCallOptions({ path: { id: selectedCallId ?? '' } }),
    enabled: Boolean(selectedCallId),
  });
  const ambulances = useQuery(listAmbulancesOptions({
    query: {
      page: 1,
      pageSize: PAGE_SIZE,
      ...(selectedCall.data?.latitude !== undefined ? { nearToLatitude: selectedCall.data.latitude } : {}),
      ...(selectedCall.data?.longitude !== undefined ? { nearToLongitude: selectedCall.data.longitude } : {}),
    },
  }));
  const currentCrew = useQuery({
    ...getCurrentAmbulanceCrewOptions({ path: { id: selectedAmbulanceId ?? '' } }),
    enabled: Boolean(selectedAmbulanceId),
  });

  function refreshOperationalBoards() {
    queryClient.invalidateQueries({
      predicate: (query) => {
        const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;
        return id === 'listEmergencyCalls' || id === 'getEmergencyCall' || id === 'listAmbulances' || id === 'getCurrentAmbulanceCrew';
      },
    });
  }

  const priorityUpdate = useMutation({
    ...updateEmergencyCallMutation(),
    onSuccess: () => {
      setNotice('Call priority updated.');
      refreshOperationalBoards();
    },
    onError: (error) => setNotice(problemMessage(error, 'Could not update the call.')),
  });

  const dispatch = useMutation({
    ...dispatchEmergencyCallMutation(),
    onSuccess: () => {
      setNotice('Ambulance assigned. Waiting for crew acknowledgement.');
      refreshOperationalBoards();
    },
    onError: (error) => {
      const conflict = isConflict(error);
      setNotice(conflict
        ? 'Another dispatcher changed this call or ambulance first. The call and fleet boards have been refreshed.'
        : problemMessage(error, 'Could not dispatch this ambulance.'));
      if (conflict) refreshOperationalBoards();
    },
  });

  const assignCrew = useMutation({
    ...assignCurrentAmbulanceCrewMutation(),
    onSuccess: () => {
      setCrewStaffId('');
      setNotice('Crew member assigned to this ambulance.');
      refreshOperationalBoards();
    },
    onError: (error) => {
      const conflict = isConflict(error);
      setNotice(conflict
        ? 'That crew assignment is no longer available. The fleet has been refreshed.'
        : problemMessage(error, 'Could not assign this crew member.'));
      if (conflict) refreshOperationalBoards();
    },
  });

  if (!canManageEmergency(role)) {
    return <AccessDenied />;
  }

  const detail = selectedCall.data;
  const assignedDispatch = detail?.dispatches?.find((item) => item.status === 'assigned');

  return (
    <>
      <h1>Emergency dispatch</h1>
      <p className="muted">Live calls, ready ambulances, and current response crews.</p>
      {notice && <p className="emergency-notice" role="status">{notice}</p>}

      <section className="card">
        <h2>Live call board</h2>
        <CallBoard
          calls={calls.data?.items ?? []}
          isLoading={calls.isLoading}
          isError={calls.isError}
          selectedId={selectedCallId}
          onSelect={setSelectedCallId}
          onRetry={() => void calls.refetch()}
        />
      </section>

      {selectedCallId && (
        <section className="card">
          <h2>Call detail</h2>
          {selectedCall.isLoading && <p className="empty">Loading call details…</p>}
          {selectedCall.isError && <Retry text="Could not load this call." onRetry={() => void selectedCall.refetch()} />}
          {detail && (
            <>
              <p><strong>{detail.caller_name ?? 'Caller details unavailable'}</strong> · {detail.status ?? 'received'}</p>
              <p className="muted">{detail.details ?? 'No caller report was recorded.'}</p>
              {detail.latitude !== undefined && detail.longitude !== undefined && (
                <p className="muted">Scene: {detail.latitude.toFixed(5)}, {detail.longitude.toFixed(5)}</p>
              )}
              <PriorityForm
                current={detail.priority ?? 'high'}
                pending={priorityUpdate.isPending}
                onSave={(priority) => priorityUpdate.mutate({ path: { id: selectedCallId }, body: { priority } })}
              />
              {assignedDispatch && <AcknowledgementCountdown dispatch={assignedDispatch} />}
              <EligibleAmbulances
                ambulances={ambulances.data?.items ?? []}
                isLoading={ambulances.isLoading}
                isError={ambulances.isError}
                dispatching={dispatch.isPending}
                onDispatch={(ambulanceId) => dispatch.mutate({ path: { id: selectedCallId }, body: { ambulance_id: ambulanceId } })}
                onRetry={() => void ambulances.refetch()}
              />
            </>
          )}
        </section>
      )}

      <section className="card">
        <h2>Fleet board</h2>
        <FleetBoard
          ambulances={ambulances.data?.items ?? []}
          isLoading={ambulances.isLoading}
          isError={ambulances.isError}
          selectedId={selectedAmbulanceId}
          onSelect={setSelectedAmbulanceId}
          onRetry={() => void ambulances.refetch()}
        />
        {selectedAmbulanceId && (
          <CrewAssignment
            crew={currentCrew.data ?? []}
            isLoading={currentCrew.isLoading}
            isError={currentCrew.isError}
            staffId={crewStaffId}
            pending={assignCrew.isPending}
            onStaffIdChange={setCrewStaffId}
            onAssign={() => assignCrew.mutate({ path: { id: selectedAmbulanceId }, body: { staff_member_id: crewStaffId.trim() } })}
            onRetry={() => void currentCrew.refetch()}
          />
        )}
      </section>
    </>
  );
}

function CallBoard({ calls, isLoading, isError, selectedId, onSelect, onRetry }: {
  calls: Array<{ id?: string; priority?: CallPriority; status?: string; caller_name?: string | null; waiting_minutes?: number }>;
  isLoading: boolean; isError: boolean; selectedId?: string; onSelect: (id: string) => void; onRetry: () => void;
}) {
  if (isLoading) return <p className="empty">Loading live calls…</p>;
  if (isError) return <Retry text="Could not load the live call board." onRetry={onRetry} />;
  if (calls.length === 0) return <p className="empty">No live calls need dispatcher attention.</p>;
  return <table><thead><tr><th>Priority</th><th>Caller</th><th>Status</th><th>Waiting</th></tr></thead><tbody>
    {calls.map((call) => call.id && <tr key={call.id} className={selectedId === call.id ? 'selected' : undefined}>
      <td><span className={`badge priority-${call.priority ?? 'high'}`}>{priorityLabels[call.priority ?? 'high']}</span></td>
      <td><button type="button" className="linklike" onClick={() => onSelect(call.id!)}>{call.caller_name ?? 'Unnamed caller'}</button></td>
      <td>{call.status ?? 'received'}</td><td>{call.waiting_minutes ?? 0} min</td>
    </tr>)}
  </tbody></table>;
}

function PriorityForm({ current, pending, onSave }: { current: CallPriority; pending: boolean; onSave: (value: CallPriority) => void }) {
  const [priority, setPriority] = useState<CallPriority>(current);
  useEffect(() => setPriority(current), [current]);
  return <form className="row emergency-form" onSubmit={(event) => { event.preventDefault(); onSave(priority); }}>
    <div><label htmlFor="call-priority">Priority</label><select id="call-priority" value={priority} onChange={(event) => setPriority(event.target.value as CallPriority)}>
      {Object.entries(priorityLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
    </select></div>
    <div className="emergency-action"><button type="submit" disabled={pending}>{pending ? 'Saving…' : 'Update priority'}</button></div>
  </form>;
}

function EligibleAmbulances({ ambulances, isLoading, isError, dispatching, onDispatch, onRetry }: {
  ambulances: Array<{ id: string; registration_number: string; is_eligible?: boolean; eligibility_block_reasons?: AmbulanceEligibilityBlockReason[] | null; distance_km?: number | null; current_crew_count?: number; required_crew_count?: number }>;
  isLoading: boolean; isError: boolean; dispatching: boolean; onDispatch: (id: string) => void; onRetry: () => void;
}) {
  return <div className="emergency-section"><h3>Ambulance choices</h3>
    {isLoading && <p className="empty">Checking ambulance eligibility…</p>}
    {isError && <Retry text="Could not check ambulance eligibility." onRetry={onRetry} />}
    {!isLoading && !isError && ambulances.length === 0 && <p className="empty">No ambulances are registered.</p>}
    {ambulances.map((ambulance) => <div className="emergency-ambulance" key={ambulance.id}>
      <div><strong>{ambulance.registration_number}</strong><span className="muted"> · crew {ambulance.current_crew_count ?? 0}/{ambulance.required_crew_count ?? 2}{ambulance.distance_km !== null && ambulance.distance_km !== undefined ? ` · ${ambulance.distance_km.toFixed(1)} km` : ''}</span>
        {!ambulance.is_eligible && <p className="emergency-block">{(ambulance.eligibility_block_reasons ?? []).map((reason) => blockReasonLabels[reason]).join(' · ') || 'Not eligible'}</p>}
      </div>
      <button type="button" disabled={!ambulance.is_eligible || dispatching} onClick={() => onDispatch(ambulance.id)}>{dispatching ? 'Dispatching…' : `Dispatch ${ambulance.registration_number}`}</button>
    </div>)}
  </div>;
}

function FleetBoard({ ambulances, isLoading, isError, selectedId, onSelect, onRetry }: {
  ambulances: Array<{ id: string; registration_number: string; status: string; current_crew_count?: number; required_crew_count?: number; location_updated_at?: string | null }>;
  isLoading: boolean; isError: boolean; selectedId?: string; onSelect: (id: string) => void; onRetry: () => void;
}) {
  if (isLoading) return <p className="empty">Loading fleet…</p>;
  if (isError) return <Retry text="Could not load the fleet board." onRetry={onRetry} />;
  if (ambulances.length === 0) return <p className="empty">No ambulances are registered.</p>;
  return <table><thead><tr><th>Ambulance</th><th>State</th><th>Crew</th><th>Location</th></tr></thead><tbody>{ambulances.map((ambulance) => <tr key={ambulance.id} className={selectedId === ambulance.id ? 'selected' : undefined}>
    <td><button type="button" className="linklike" onClick={() => onSelect(ambulance.id)}>{ambulance.registration_number}</button></td><td>{ambulance.status}</td><td>{ambulance.current_crew_count ?? 0}/{ambulance.required_crew_count ?? 2}</td><td>{ambulance.location_updated_at ? new Date(ambulance.location_updated_at).toLocaleString() : 'Unknown'}</td>
  </tr>)}</tbody></table>;
}

function CrewAssignment({ crew, isLoading, isError, staffId, pending, onStaffIdChange, onAssign, onRetry }: {
  crew: Array<{ staff_member_id?: string; full_name?: string | null }>;
  isLoading: boolean; isError: boolean; staffId: string; pending: boolean; onStaffIdChange: (value: string) => void; onAssign: () => void; onRetry: () => void;
}) {
  function submit(event: FormEvent) { event.preventDefault(); onAssign(); }
  return <div className="emergency-section"><h3>Current crew assignment</h3>
    {isLoading && <p className="empty">Loading current crew…</p>}
    {isError && <Retry text="Could not load current crew." onRetry={onRetry} />}
    {!isLoading && !isError && <ul className="emergency-crew">{crew.length ? crew.map((member) => <li key={member.staff_member_id}>{member.full_name ?? member.staff_member_id}</li>) : <li>No crew assigned yet.</li>}</ul>}
    <form className="row emergency-form" onSubmit={submit}><div><label htmlFor="crew-staff-id">Ambulance crew staff ID</label><input id="crew-staff-id" value={staffId} onChange={(event) => onStaffIdChange(event.target.value)} placeholder="Staff UUID" required /></div><div className="emergency-action"><button type="submit" disabled={pending || !staffId.trim()}>{pending ? 'Assigning…' : 'Assign crew member'}</button></div></form>
  </div>;
}

function AcknowledgementCountdown({ dispatch }: { dispatch: DispatchSummary }) {
  const [now, setNow] = useState(Date.now());
  useEffect(() => { const timer = window.setInterval(() => setNow(Date.now()), 1000); return () => window.clearInterval(timer); }, []);
  const sentAt = dispatch.dispatched_at ? new Date(dispatch.dispatched_at).getTime() : now;
  const remaining = Math.max(0, Math.ceil((acknowledgementWindowMs - (now - sentAt)) / 1000));
  return <p className={remaining === 0 ? 'emergency-warning' : 'emergency-countdown'} role="status">{remaining === 0 ? 'Crew has not acknowledged. Dispatcher attention required.' : `Crew acknowledgement warning in ${remaining}s.`}</p>;
}

function Retry({ text, onRetry }: { text: string; onRetry: () => void }) {
  return <div className="empty"><p>{text}</p><button type="button" className="secondary" onClick={onRetry}>Try again</button></div>;
}

function AccessDenied() {
  return <section className="card"><h1>Emergency dispatch</h1><p className="muted">Only Duty Managers can view calls, manage crews, or dispatch an ambulance.</p></section>;
}

function isConflict(error: unknown): boolean {
  return typeof error === 'object' && error !== null && ('status' in error && (error as { status?: number }).status === 409 || 'response' in error && (error as { response?: Response }).response?.status === 409);
}

function problemMessage(error: unknown, fallback: string): string {
  if (typeof error !== 'object' || error === null) return fallback;
  const value = error as { detail?: unknown; message?: unknown; error?: { detail?: unknown } };
  return typeof value.detail === 'string' ? value.detail : typeof value.error?.detail === 'string' ? value.error.detail : typeof value.message === 'string' ? value.message : fallback;
}
