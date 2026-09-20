import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  assignBedManuallyMutation,
  getBedWorkflowOptions,
  requestBedSuggestionMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { PrincipalRole, SuggestedBed } from '../services/api/generated';
import { canSetHighCareLevel } from '../types/permissions';
import { admissionCategoryLabels, admissionUrgencyLabels, genderLabels } from '../types/patients';

const POLL_INTERVAL_MS = 1500;

/**
 * The bed agent's suggestion, from either a row already on the board or an NIC/patient code
 * typed at the desk. Confirming any bed here — the best one or an alternative — calls the same
 * `assign-bed` endpoint a manual pick calls (§8.6b): there is no separate approval step.
 */
export function BedSuggestionPanel({
  admissionId,
  role,
  onAssigned,
  onClose,
}: {
  /** Known already, from a row on the patients board. Omit to start from a typed identifier. */
  admissionId?: string;
  role: PrincipalRole | undefined;
  onAssigned: () => void;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [identifier, setIdentifier] = useState('');
  const [workflowId, setWorkflowId] = useState<string | null>(null);
  const [reason, setReason] = useState('');

  const start = useMutation({
    ...requestBedSuggestionMutation(),
    onSuccess: (accepted) => {
      if (accepted.workflow_id) {
        setWorkflowId(accepted.workflow_id);
      }
    },
  });

  const workflow = useQuery({
    ...getBedWorkflowOptions({ path: { workflowId: workflowId ?? '' } }),
    enabled: workflowId !== null,
    refetchInterval: (query) => (query.state.data?.status === 'running' ? POLL_INTERVAL_MS : false),
  });

  const assign = useMutation({
    ...assignBedManuallyMutation(),
    onSuccess: (assignment) => {
      toast.success(
        `${assignment.ward_name} · ${assignment.bed_number} is held for ` +
          `${workflow.data?.patient?.full_name ?? 'the patient'}. The hold expires if they are ` +
          'not marked as arrived.',
      );

      queryClient.invalidateQueries({
        predicate: (q) => {
          const id = (q.queryKey[0] as { _id?: string } | undefined)?._id;

          return (
            id === 'listPatientWorklist' ||
            id === 'listAdmissions' ||
            id === 'getAdmission' ||
            id === 'listBedAvailability' ||
            id === 'getWardCapacity' ||
            id === 'getWardOccupancy'
          );
        },
      });

      onAssigned();
    },
  });

  function submitIdentifier(event: FormEvent) {
    event.preventDefault();
    if (identifier.trim().length === 0) return;
    start.mutate({ body: { patient_identifier: identifier.trim() } });
  }

  function confirm(bed: SuggestedBed) {
    const targetAdmissionId = admissionId ?? workflow.data?.patient?.admission_id;
    if (!targetAdmissionId || !workflowId) return;

    assign.mutate({
      path: { id: targetAdmissionId },
      body: {
        bed_id: bed.bed_id,
        workflow_id: workflowId,
        override_reason: reason.trim().length > 0 ? reason.trim() : undefined,
      },
    });
  }

  // From a row on the board, the admission is already known, so the run starts as soon as the
  // panel opens rather than asking the nurse to type an identifier they already found once.
  useEffect(() => {
    if (admissionId) {
      start.mutate({ body: { admission_id: admissionId } });
    }
  }, [admissionId]);

  if (!admissionId && workflowId === null) {
    return (
      <div className="drawer-body">
        <h3>Suggest a bed</h3>
        <p className="muted">
          Type the NIC or patient code off the hospital slip. The agent looks up who they are and
          suggests a bed the way it would from a row on the board.
        </p>

        <form onSubmit={submitIdentifier}>
          <div className="field">
            <label htmlFor="bed-suggestion-identifier">NIC or patient code</label>
            <input
              id="bed-suggestion-identifier"
              value={identifier}
              maxLength={100}
              onChange={(event) => setIdentifier(event.target.value)}
              placeholder="e.g. 200012345678 or PT-7K4M2Q"
            />
          </div>

          <div style={{ display: 'flex', gap: '0.5rem' }}>
            <button type="submit" disabled={start.isPending || identifier.trim().length === 0}>
              {start.isPending ? 'Starting…' : 'Suggest a bed'}
            </button>
            <button type="button" className="secondary" onClick={onClose}>
              Close
            </button>
          </div>
        </form>
      </div>
    );
  }

  const running = workflow.data?.status === 'running' || workflow.data === undefined;

  return (
    <div className="drawer-body">
      <h3>Bed suggestion</h3>

      {(workflow.isLoading || running) && (
        <p className="empty">The agent is working on this — usually a few seconds.</p>
      )}

      {workflow.isError && (
        <div className="empty">
          <p>Could not load the suggestion run.</p>
          <button type="button" className="secondary" onClick={() => void workflow.refetch()}>
            Try again
          </button>
        </div>
      )}

      {workflow.data && !running && (
        <>
          {workflow.data.patient && (
            <table>
              <tbody>
                <tr>
                  <th scope="row">Patient</th>
                  <td>
                    {workflow.data.patient.full_name}
                    {workflow.data.patient.patient_code && (
                      <>
                        {' '}
                        <code>{workflow.data.patient.patient_code}</code>
                      </>
                    )}
                  </td>
                </tr>
                {workflow.data.patient.gender && (
                  <tr>
                    <th scope="row">Gender</th>
                    <td>{genderLabels[workflow.data.patient.gender]}</td>
                  </tr>
                )}
                {workflow.data.patient.admission_category && (
                  <tr>
                    <th scope="row">Care level</th>
                    <td>{admissionCategoryLabels[workflow.data.patient.admission_category]}</td>
                  </tr>
                )}
                {workflow.data.patient.urgency && (
                  <tr>
                    <th scope="row">Urgency</th>
                    <td>{admissionUrgencyLabels[workflow.data.patient.urgency]}</td>
                  </tr>
                )}
              </tbody>
            </table>
          )}

          {workflow.data.blocker ? (
            <p className="empty" style={{ marginTop: '0.9rem' }}>
              {workflow.data.blocker.message}
            </p>
          ) : (
            <>
              {[
                workflow.data.best ? { ...workflow.data.best, label: 'Suggested' } : null,
                ...(workflow.data.alternatives ?? []).map((bed) => ({
                  ...bed,
                  label: 'Alternative',
                })),
              ]
                .filter((bed): bed is SuggestedBed & { label: string } => bed !== null)
                .map((bed) => {
                  const hidden = bed.requires_duty_manager && !canSetHighCareLevel(role);

                  return (
                    <div
                      key={bed.bed_id}
                      className="card"
                      style={{ marginTop: '0.75rem', padding: '0.75rem' }}
                    >
                      <p style={{ margin: 0 }}>
                        <strong>{bed.label}:</strong> {bed.ward_name} · {bed.bed_number}
                        {bed.is_downgrade && (
                          <span className="muted"> — a downgrade from the requested level</span>
                        )}
                      </p>
                      {bed.rationale && (
                        <p className="muted" style={{ marginTop: '0.25rem' }}>
                          {bed.rationale}
                        </p>
                      )}
                      {!hidden && (
                        <button
                          type="button"
                          style={{ marginTop: '0.5rem' }}
                          disabled={assign.isPending}
                          onClick={() => confirm(bed)}
                        >
                          {assign.isPending ? 'Assigning…' : 'Use this bed'}
                        </button>
                      )}
                    </div>
                  );
                })}
            </>
          )}

          <div className="field" style={{ marginTop: '0.9rem' }}>
            <label htmlFor="bed-suggestion-reason">Note (optional)</label>
            <input
              id="bed-suggestion-reason"
              value={reason}
              maxLength={500}
              onChange={(event) => setReason(event.target.value)}
              placeholder="Kept on the record"
            />
          </div>
        </>
      )}

      <button type="button" className="secondary" style={{ marginTop: '0.9rem' }} onClick={onClose}>
        Close
      </button>
    </div>
  );
}
