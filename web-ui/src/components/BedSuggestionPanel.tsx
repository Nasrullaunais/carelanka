import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  assignBedManuallyMutation,
  getBedWorkflowOptions,
  getPatientOptions,
  listBedAvailabilityOptions,
  listWardsOptions,
  requestBedSuggestionMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { PrincipalRole, SuggestedBed } from '../services/api/generated';
import { canSetHighCareLevel } from '../types/permissions';
import { placementFor } from '../types/beds';
import type { Placement } from '../types/beds';
import { admissionCategoryLabels, admissionUrgencyLabels, genderLabels } from '../types/patients';
import { AgentProgress } from './AgentProgress';
import { BedCandidateTable } from './BedCandidateTable';
import type { BedCandidateBed } from './BedCandidateTable';

const POLL_INTERVAL_MS = 800;

/**
 * The plan is fixed and known before a single tool runs (§8.7), so the caption for each step is
 * decided once here rather than guessed at from whatever text a model might have written.
 */
const STEP_CAPTIONS: Record<string, string> = {
  resolve_patient: 'Looking up the patient',
  read_admission_requirements: 'Reading their admission details',
  list_candidate_beds: 'Checking which beds are free',
  apply_hard_rules: "Applying the hospital's placement rules",
  rank_on_soft_rules: 'Ranking beds by fit',
  decide_best_and_alternatives: 'Shortlisting the best options',
  weigh_patient_notes: "Reading the clinician's notes",
  validate_deterministically: 'Double-checking the suggestion',
  pause_for_approval: 'Wrapping up',
};

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
  const [showAlternatives, setShowAlternatives] = useState(false);

  const start = useMutation({
    ...requestBedSuggestionMutation(),
    onSuccess: (accepted) => {
      if (accepted.workflow_id) {
        setWorkflowId(accepted.workflow_id);
        setShowAlternatives(false);
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

  function pickBed(bed: BedCandidateBed) {
    const targetAdmissionId = admissionId ?? workflow.data?.patient?.admission_id;
    if (!targetAdmissionId || !workflowId) return;

    assign.mutate({
      path: { id: targetAdmissionId },
      body: {
        bed_id: bed.id,
        workflow_id: workflowId,
        override_reason: reason.trim().length > 0 ? reason.trim() : undefined,
      },
    });
  }

  const patient = workflow.data?.patient;

  const patientDetails = useQuery({
    ...getPatientOptions({ path: { id: patient?.patient_id ?? '' } }),
    enabled: showAlternatives && !!patient?.patient_id,
  });

  const wards = useQuery({ ...listWardsOptions({}), enabled: showAlternatives });

  const allBeds = useQuery({
    ...listBedAvailabilityOptions({ query: { availability: 'free', pageSize: 500 } }),
    enabled: showAlternatives,
  });

  const wardsById = new Map((wards.data ?? []).map((ward) => [ward.id, ward]));

  const bedCandidates = (allBeds.data?.items ?? []).map((bed) => ({
    bed,
    ward: wardsById.get(bed.ward_id),
    placement:
      patient?.admission_category !== undefined
        ? placementFor(
            bed,
            wardsById.get(bed.ward_id),
            { admission_category: patient.admission_category, is_infectious: patient.is_infectious ?? false },
            { gender: patient.gender, date_of_birth: patientDetails.data?.date_of_birth },
            role,
          )
        : ({ kind: 'refused', why: 'Loading…' } as Placement),
  }));

  const bedsMissing = (allBeds.data?.total_items ?? 0) - (allBeds.data?.items?.length ?? 0);
  const bedTableLoading = wards.isLoading || allBeds.isLoading || patientDetails.isLoading;
  const bedTableFailed = wards.isError || allBeds.isError || patientDetails.isError;

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
  const alternatives = workflow.data?.alternatives ?? [];

  return (
    <div className="drawer-body">
      <h3>Bed suggestion</h3>

      {(workflow.isLoading || running) && (
        <AgentProgress plan={workflow.data?.plan} steps={workflow.data?.steps} captions={STEP_CAPTIONS} />
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
                <tr>
                  <th scope="row">Needs isolation</th>
                  <td>{workflow.data.patient.is_infectious ? 'Yes' : 'No'}</td>
                </tr>
              </tbody>
            </table>
          )}

          {workflow.data.blocker ? (
            <p className="empty" style={{ marginTop: '0.9rem' }}>
              {workflow.data.blocker.message}
            </p>
          ) : (
            <>
              {workflow.data.best && (
                <BedCard
                  bed={workflow.data.best}
                  headline="The agent suggests"
                  role={role}
                  assigning={assign.isPending}
                  onUse={confirm}
                />
              )}

              {alternatives.length > 0 && !showAlternatives && (
                <button
                  type="button"
                  className="secondary"
                  style={{ marginTop: '0.75rem' }}
                  onClick={() => setShowAlternatives(true)}
                >
                  Choose another bed ({alternatives.length} other{' '}
                  {alternatives.length === 1 ? 'bed fits' : 'beds fit'})
                </button>
              )}

              {showAlternatives && (
                <div style={{ marginTop: '0.75rem' }}>
                  {bedTableLoading ? (
                    <p className="empty">Loading…</p>
                  ) : bedTableFailed ? (
                    <div className="empty">
                      <p>Could not load the free beds.</p>
                      <button
                        type="button"
                        className="secondary"
                        onClick={() => {
                          void wards.refetch();
                          void allBeds.refetch();
                          void patientDetails.refetch();
                        }}
                      >
                        Try again
                      </button>
                    </div>
                  ) : (
                    <BedCandidateTable
                      candidates={bedCandidates}
                      writing={assign.isPending}
                      missing={bedsMissing}
                      actionLabel={(placement) =>
                        placement.kind === 'override' ? 'Use anyway' : 'Use this bed'
                      }
                      onPick={(bed) => pickBed(bed)}
                    />
                  )}
                </div>
              )}
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

/**
 * One selectable bed. The suggested bed and every alternative use the same card on purpose — the
 * agent's pick is committed by exactly the same button as a bed a nurse picked themselves.
 */
function BedCard({
  bed,
  headline,
  role,
  assigning,
  onUse,
}: {
  bed: SuggestedBed;
  headline: string;
  role: PrincipalRole | undefined;
  assigning: boolean;
  onUse: (bed: SuggestedBed) => void;
}) {
  // A ward nurse cannot commit an ICU, HDU or downgraded bed, so they are not offered the button.
  const hidden = bed.requires_duty_manager && !canSetHighCareLevel(role);

  return (
    <div className="card" style={{ marginTop: '0.75rem', padding: '0.75rem' }}>
      <p className="muted" style={{ margin: 0, fontSize: '0.85rem' }}>
        {headline}
      </p>
      <p style={{ margin: '0.15rem 0 0', fontSize: '1.05rem' }}>
        <strong>
          {bed.ward_name} · {bed.bed_number}
        </strong>
        {bed.is_downgrade && (
          <span className="muted"> — a downgrade from the requested level</span>
        )}
      </p>

      {bed.rationale && <p style={{ marginTop: '0.35rem' }}>{bed.rationale}</p>}

      {(bed.fit_factors ?? []).length > 0 && (
        <ul className="muted" style={{ marginTop: '0.35rem', paddingLeft: '1.1rem' }}>
          {bed.fit_factors!.map((factor) => (
            <li key={factor}>{factor}</li>
          ))}
        </ul>
      )}

      {hidden ? (
        <p className="muted" style={{ marginTop: '0.5rem' }}>
          A Duty Manager has to place this one.
        </p>
      ) : (
        <button
          type="button"
          style={{ marginTop: '0.5rem' }}
          disabled={assigning}
          onClick={() => onUse(bed)}
        >
          {assigning ? 'Assigning…' : 'Use this bed'}
        </button>
      )}
    </div>
  );
}

