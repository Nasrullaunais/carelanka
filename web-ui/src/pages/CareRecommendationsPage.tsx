import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  approveCareRecommendationMutation,
  getCareRecommendationOptions,
  getCareWorkflowOptions,
  getPatientMedicalProfileOptions,
  getPatientOptions,
  listCareRecommendationsOptions,
  redraftCareRecommendationMutation,
  rejectCareRecommendationMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type {
  BedAgentStep,
  CareRecommendationStatus,
  CareUrgency,
} from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { canReadCareQueue, canReviewCareRecommendation } from '../types/permissions';

const urgencyLabels: Record<CareUrgency, string> = {
  low: 'Low',
  medium: 'Medium',
  high: 'High',
};

const statusLabels: Record<CareRecommendationStatus, string> = {
  pending_review: 'Pending review',
  approved: 'Approved',
  rejected: 'Rejected',
};

const POLL_INTERVAL_MS = 800;

/**
 * Plain-language captions for the agent's own step names, so a nurse watching the run sees what
 * is happening rather than the identifiers the workflow stores.
 */
const STEP_CAPTIONS: Record<string, string> = {
  screen_red_flags: 'Checking the report for warning signs',
  get_medical_profile: 'Reading the medical profile',
  get_patient_history: 'Reading past visits and reports',
  get_current_admission: 'Reading the current admission',
  draft_recommendation: 'Writing a draft note',
  validate_deterministically: 'Safety-checking the draft',
  pause_for_approval: 'Waiting for a reviewer',
};

const statusFilters: Array<{ value: CareRecommendationStatus | 'all'; label: string }> = [
  { value: 'pending_review', label: 'Pending review' },
  { value: 'approved', label: 'Approved' },
  { value: 'rejected', label: 'Rejected' },
  { value: 'all', label: 'All' },
];

export function CareRecommendationsPage() {
  const session = useSession();
  const role = session?.principal.role;
  const canRead = canReadCareQueue(role);

  const [status, setStatus] = useState<CareRecommendationStatus | 'all'>('pending_review');
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const queue = useQuery({
    ...listCareRecommendationsOptions({
      query: { status: status === 'all' ? undefined : status, pageSize: 50, sortDir: 'desc' },
    }),
    enabled: canRead,
  });

  if (!canRead) {
    return (
      <>
        <h1>Care recommendations</h1>
        <p className="empty">
          Your role cannot open the care advisory queue. Ward nurses, doctors and the duty
          manager can.
        </p>
      </>
    );
  }

  const rows = queue.data?.items ?? [];

  return (
    <>
      <h1>Care recommendations</h1>
      <p className="muted">
        What patients have reported about how they feel, and the agent&apos;s draft note for a
        nurse or doctor to check before anything reaches the patient.
      </p>

      <div className="card">
        <div className="filters">
          {statusFilters.map((filter) => (
            <button
              key={filter.value}
              type="button"
              className={filter.value === status ? '' : 'secondary'}
              onClick={() => setStatus(filter.value)}
            >
              {filter.label}
            </button>
          ))}
        </div>

        {queue.isError ? (
          <div className="empty">
            <p>Could not load the review queue.</p>
            <button type="button" className="secondary" onClick={() => void queue.refetch()}>
              Try again
            </button>
          </div>
        ) : queue.isLoading ? (
          <p className="empty">Loading…</p>
        ) : rows.length === 0 ? (
          <p className="empty">Nothing here right now.</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Patient</th>
                <th>Reported</th>
                <th>Red flag</th>
                <th>Urgency</th>
                <th>Status</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.id} className={row.id === selectedId ? 'open' : undefined}>
                  <td>
                    <PatientName patientId={row.patient_id ?? ''} />
                  </td>
                  <td className="small">
                    {row.reported_at ? new Date(row.reported_at).toLocaleString() : '—'}
                  </td>
                  <td>
                    {row.red_flag && <span className="badge severity-high">Red flag</span>}
                  </td>
                  <td>
                    {row.urgency_flag ? (
                      <span className={`badge severity-${row.urgency_flag}`}>
                        {urgencyLabels[row.urgency_flag]}
                      </span>
                    ) : (
                      <span className="muted">—</span>
                    )}
                  </td>
                  <td>
                    <span
                      className={`badge ${row.status === 'approved' ? 'status-available' : row.status === 'rejected' ? 'status-retired' : ''}`}
                    >
                      {row.status ? statusLabels[row.status] : '—'}
                    </span>
                  </td>
                  <td>
                    <button
                      type="button"
                      className="secondary small"
                      onClick={() => setSelectedId(row.id === selectedId ? null : (row.id ?? null))}
                    >
                      {row.id === selectedId ? 'Close' : 'Open'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {selectedId && (
        <div className="card">
          <RecommendationDetail
            recommendationId={selectedId}
            onDone={() => setSelectedId(null)}
          />
        </div>
      )}
    </>
  );
}

function PatientName({ patientId }: { patientId: string }) {
  const patient = useQuery({ ...getPatientOptions({ path: { id: patientId } }), enabled: !!patientId });

  if (!patientId || patient.isLoading) {
    return <span className="muted">Loading…</span>;
  }

  if (patient.isError || !patient.data) {
    return <span className="muted">Unknown patient</span>;
  }

  return (
    <>
      <strong>{patient.data.full_name}</strong>
      <div className="small muted">{patient.data.patient_code}</div>
    </>
  );
}

function RecommendationDetail({
  recommendationId,
  onDone,
}: {
  recommendationId: string;
  onDone: () => void;
}) {
  const session = useSession();
  const role = session?.principal.role;
  const queryClient = useQueryClient();

  const [doctorMessage, setDoctorMessage] = useState<string | null>(null);
  const [rejectionReason, setRejectionReason] = useState('');
  const [showReject, setShowReject] = useState(false);

  const detail = useQuery({
    ...getCareRecommendationOptions({ path: { id: recommendationId } }),
    // Keep asking until the background run puts a draft on the row, so the note appears on its
    // own rather than only when the reviewer thinks to reload.
    refetchInterval: (query) =>
      query.state.data?.workflow_id && !query.state.data?.agent_message
        ? POLL_INTERVAL_MS
        : false,
  });

  const patientId = detail.data?.patient_id ?? '';
  const patient = useQuery({ ...getPatientOptions({ path: { id: patientId } }), enabled: !!patientId });
  const profile = useQuery({
    ...getPatientMedicalProfileOptions({ path: { id: patientId } }),
    enabled: !!patientId,
  });

  // The draft is written by a background run, so an report opened straight away has no note yet.
  // Polling the workflow is what tells the reviewer it is still coming rather than absent.
  const workflowId = detail.data?.workflow_id ?? '';
  const workflow = useQuery({
    ...getCareWorkflowOptions({ path: { workflowId } }),
    enabled: !!workflowId,
    refetchInterval: (query) =>
      query.state.data?.status === 'running' ? POLL_INTERVAL_MS : false,
  });

  const agentRunning = !!workflowId && (workflow.isLoading || workflow.data?.status === 'running');

  const refresh = () =>
    queryClient.invalidateQueries({
      predicate: (query) => {
        const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;

        return id === 'listCareRecommendations' || id === 'getCareRecommendation';
      },
    });

  const approve = useMutation({
    ...approveCareRecommendationMutation(),
    onSuccess: () => {
      toast.success('Approved. The patient can now see this.');
      void refresh();
      onDone();
    },
  });

  const reject = useMutation({
    ...rejectCareRecommendationMutation(),
    onSuccess: () => {
      toast.success('Rejected.');
      void refresh();
      onDone();
    },
  });

  const redraft = useMutation({
    ...redraftCareRecommendationMutation(),
    onSuccess: () => {
      toast.success('Running the agent again…');
      setDoctorMessage(null);
      void detail.refetch();
      void workflow.refetch();
    },
  });

  if (detail.isLoading) {
    return <p className="empty">Loading…</p>;
  }

  if (detail.isError || !detail.data) {
    return (
      <div className="empty">
        <p>Could not load this report.</p>
        <button type="button" className="secondary" onClick={() => void detail.refetch()}>
          Try again
        </button>
      </div>
    );
  }

  const row = detail.data;
  const mayReview = canReviewCareRecommendation(role) && row.status === 'pending_review';
  const draftedMessage = doctorMessage ?? row.agent_message ?? '';

  return (
    <div className="drawer-body">
      <div className="dialog-head no-print">
        <h3>
          {patient.data?.full_name ?? 'Patient'}{' '}
          {row.red_flag && <span className="badge severity-high">Red flag</span>}
        </h3>
        <div className="actions">
          <button type="button" className="secondary small" onClick={onDone}>
            Close
          </button>
        </div>
      </div>

      <dl className="detail-grid">
        <div>
          <dt>Reported</dt>
          <dd>{row.reported_at ? new Date(row.reported_at).toLocaleString() : '—'}</dd>
        </div>
        <div>
          <dt>Urgency (agent)</dt>
          <dd>{row.urgency_flag ? urgencyLabels[row.urgency_flag] : <span className="muted">—</span>}</dd>
        </div>
        <div>
          <dt>Status</dt>
          <dd>{row.status ? statusLabels[row.status] : '—'}</dd>
        </div>
      </dl>

      <h4>What the patient said</h4>
      <p>{row.reported_text}</p>

      <h4>What the agent read</h4>
      {profile.isLoading ? (
        <p className="empty">Loading…</p>
      ) : !profile.data ||
        (!profile.data.known_conditions &&
          !profile.data.allergies &&
          !profile.data.current_symptoms &&
          !profile.data.recent_situation) ? (
        <p className="muted">No medical profile is on record for this patient.</p>
      ) : (
        <dl className="detail-grid">
          <div>
            <dt>Known conditions</dt>
            <dd>{profile.data.known_conditions || <span className="muted">Not recorded</span>}</dd>
          </div>
          <div>
            <dt>Allergies</dt>
            <dd>{profile.data.allergies || <span className="muted">Not recorded</span>}</dd>
          </div>
          <div>
            <dt>Current symptoms</dt>
            <dd>{profile.data.current_symptoms || <span className="muted">Not recorded</span>}</dd>
          </div>
          <div>
            <dt>Recent situation</dt>
            <dd>{profile.data.recent_situation || <span className="muted">Not recorded</span>}</dd>
          </div>
        </dl>
      )}

      <h4>The agent&apos;s draft</h4>
      <p className="hint">Staff-facing only. Never shown to the patient as written here.</p>

      {agentRunning && !row.agent_message ? (
        <>
          <p className="muted">The agent is working on this now…</p>
          <AgentProgress plan={workflow.data?.plan} steps={workflow.data?.steps} />
        </>
      ) : (
        <>
          {workflow.data && workflow.data.draft_source !== 'model' && (
            <div className="hint" role="status" style={{ color: '#8a5300' }}>
              <p style={{ margin: 0 }}>
                <strong>Not a note about this patient.</strong>{' '}
                {workflow.data.draft_note ??
                  'The standard backup note was used instead of an AI draft.'}
              </p>
            </div>
          )}
          <p>
            {row.agent_message || (
              <span className="muted">No draft — reviewer decides from the report alone.</span>
            )}
          </p>

          {mayReview && (
            <button
              type="button"
              className="secondary small"
              disabled={redraft.isPending}
              onClick={() => redraft.mutate({ path: { id: recommendationId } })}
            >
              {redraft.isPending ? 'Starting…' : 'Try the agent again'}
            </button>
          )}
        </>
      )}

      {row.status !== 'pending_review' && (
        <>
          <h4>Reviewed</h4>
          <dl className="detail-grid">
            {row.status === 'approved' && (
              <div>
                <dt>Message sent to the patient</dt>
                <dd>{row.doctor_message}</dd>
              </div>
            )}
            {row.status === 'rejected' && (
              <div>
                <dt>Rejection reason (staff-facing only)</dt>
                <dd>{row.rejection_reason}</dd>
              </div>
            )}
          </dl>
        </>
      )}

      {mayReview && !showReject && (
        <div className="no-print" style={{ marginTop: '0.9rem' }}>
          <div className="field">
            <label htmlFor="doctor-message">Message the patient will see</label>
            <textarea
              id="doctor-message"
              rows={3}
              maxLength={2000}
              value={draftedMessage}
              onChange={(event) => setDoctorMessage(event.target.value)}
            />
            <p className="hint">
              Leave the agent&apos;s draft as-is, or edit it. Nothing reaches the patient until
              you approve.
            </p>
          </div>

          <div className="actions">
            <button
              type="button"
              disabled={approve.isPending}
              onClick={() =>
                approve.mutate({
                  path: { id: recommendationId },
                  body: { doctor_message: draftedMessage.trim() || null },
                })
              }
            >
              {approve.isPending ? 'Approving…' : 'Approve'}
            </button>
            <button type="button" className="secondary" onClick={() => setShowReject(true)}>
              Reject
            </button>
          </div>
        </div>
      )}

      {mayReview && showReject && (
        <div className="no-print" style={{ marginTop: '0.9rem' }}>
          <div className="field">
            <label htmlFor="rejection-reason">Reason for rejecting</label>
            <textarea
              id="rejection-reason"
              rows={2}
              maxLength={1000}
              value={rejectionReason}
              placeholder="Nothing clinically new; already covered on the ward round."
              onChange={(event) => setRejectionReason(event.target.value)}
            />
            <p className="hint">Staff-facing only. The patient never sees this.</p>
          </div>

          <div className="actions">
            <button
              type="button"
              disabled={reject.isPending || rejectionReason.trim().length < 3}
              onClick={() =>
                reject.mutate({
                  path: { id: recommendationId },
                  body: { reason: rejectionReason.trim() },
                })
              }
            >
              {reject.isPending ? 'Rejecting…' : 'Confirm reject'}
            </button>
            <button type="button" className="secondary" onClick={() => setShowReject(false)}>
              Back
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

/**
 * What the agent has actually done so far. `plan` comes back the moment the run starts and
 * `steps` fills in as each one completes, so this is the real state of the run rather than a
 * spinner timed to feel about right.
 */
function AgentProgress({
  plan,
  steps,
}: {
  plan: string[] | null | undefined;
  steps: BedAgentStep[] | null | undefined;
}) {
  const order = plan && plan.length > 0 ? plan : Object.keys(STEP_CAPTIONS);
  const done = new Set((steps ?? []).filter((step) => step.ok !== false).map((step) => step.step));
  const currentIndex = order.findIndex((step) => !done.has(step));

  return (
    <ul className="agent-progress" style={{ listStyle: 'none', margin: '0.5rem 0 0', padding: 0 }}>
      {order.map((step, index) => {
        const isDone = done.has(step);
        const isCurrent = !isDone && index === currentIndex;

        return (
          <li
            key={step}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '0.5rem',
              padding: '0.2rem 0',
              opacity: isDone || isCurrent ? 1 : 0.45,
            }}
          >
            <span aria-hidden="true">{isDone ? '✓' : isCurrent ? '…' : '·'}</span>
            <span className={isCurrent ? '' : 'muted'}>
              {STEP_CAPTIONS[step] ?? step}
              {isCurrent ? '…' : ''}
            </span>
          </li>
        );
      })}
    </ul>
  );
}
