import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  createAdmissionMutation,
  createPatientMutation,
  getPatientOptions,
  lookupPatientMutation,
  updatePatientMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type {
  Admission,
  AdmissionCategory,
  AdmissionUrgency,
  Patient,
  PatientSummary,
} from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { nicProblem } from '../types/identifiers';
import {
  PatientFields,
  emptyPatientForm,
  onSubmit,
  patientFormBody,
  patientFormFrom,
  patientFormProblems,
  usePatientForm,
} from './intake-form';
import { canEditPatient, canRegisterPatient } from '../types/permissions';
import {
  admissionCategories,
  admissionCategoryHints,
  admissionCategoryLabels,
  admissionUrgencies,
  admissionUrgencyLabels,
  detailFieldLabel,
  genderLabels,
  patientIdentifier,
} from '../types/patients';

// Someone has walked up to the desk. Three steps, in this order on purpose:
//
//   find  ->  register (only if they are new)  ->  admit
//
// The lookup is first because a returning patient must keep one record with many admissions.
// A second record for the same person is the most damaging data problem in this component,
// and it stays invisible until somebody needs the history.

type Step = 'find' | 'register' | 'edit' | 'admit' | 'done';

// 'edit' is the same form as 'register', pointed at PUT /patients/{id} instead of POST.
//
// It exists because registering somebody WRITES THEM TO THE DATABASE, and "Start over" only
// clears the boxes on screen. Somebody who misspelt a name, pressed Register and then pressed
// Start over had no way back to it at all: the next lookup found the record and offered to
// admit it, misspelling and all. A form that can create a record and not correct one is a form
// that turns every typo into a permanent one.

export function IntakePage() {
  const session = useSession();
  const role = session?.principal.role;

  const [step, setStep] = useState<Step>('find');
  const [patient, setPatient] = useState<Patient | PatientSummary | null>(null);
  const [knownNic, setKnownNic] = useState('');
  const [admission, setAdmission] = useState<Admission | null>(null);

  // Where "Cancel" and "Save" go back to. Editing is reached from two places and they are not
  // the same place: from the admit step it is a detour, and going back to admit is right. From
  // the lookup it is a correction on somebody who may already be in a bed, and dropping them on
  // the admit step would offer an Admit button that the server refuses with cl_pat_006.
  const [editReturn, setEditReturn] = useState<'find' | 'admit'>('admit');

  function leaveEdit() {
    if (editReturn === 'find') {
      // Back to the lookup, not to the record they were just editing. The lookup is the screen
      // that knows whether this person can be admitted, and it asks the server again.
      restart();
      return;
    }

    setStep('admit');
  }

  function restart() {
    setStep('find');
    setPatient(null);
    setKnownNic('');
    setAdmission(null);
  }

  if (!canRegisterPatient(role)) {
    return (
      <>
        <h1>Walk-in intake</h1>
        <p className="empty">
          Your role cannot register or admit patients. Ward nurses, ambulance crew and the
          duty manager can.
        </p>
      </>
    );
  }

  return (
    <>
      <h1>Walk-in intake</h1>
      <p className="muted">
        Register a patient who has arrived at the desk, then admit them. Search first — a
        returning patient keeps one record.
      </p>

      <Steps current={step} />

      {step === 'find' && (
        <FindStep
          canEdit={canEditPatient(role)}
          onExisting={(found) => {
            setPatient(found);
            setStep('admit');
          }}
          onEditExisting={(found) => {
            setPatient(found);
            setEditReturn('find');
            setStep('edit');
          }}
          onNew={(nic) => {
            setKnownNic(nic);
            setStep('register');
          }}
        />
      )}

      {step === 'register' && (
        <RegisterStep
          nic={knownNic}
          onBack={restart}
          onRegistered={(created) => {
            setPatient(created);
            setStep('admit');
          }}
        />
      )}

      {step === 'edit' && patient && (
        <EditStep
          patientId={patient.id}
          onBack={leaveEdit}
          onSaved={(saved) => {
            setPatient(saved);
            leaveEdit();
          }}
        />
      )}

      {step === 'admit' && patient && (
        <AdmitStep
          patient={patient}
          staffId={session?.principal.id ?? ''}
          canEdit={canEditPatient(role)}
          onEdit={() => {
            setEditReturn('admit');
            setStep('edit');
          }}
          onBack={restart}
          onAdmitted={(created) => {
            setAdmission(created);
            setStep('done');
          }}
        />
      )}

      {step === 'done' && admission && <DoneStep admission={admission} onAnother={restart} />}
    </>
  );
}

function Steps({ current }: { current: Step }) {
  const order: Step[] = ['find', 'register', 'admit'];
  const labels: Record<Step, string> = {
    find: '1. Search',
    register: '2. Register',
    // Correcting details is a detour off step 3, not a step of its own - so it lights up
    // "Register", which is the step whose work is being redone.
    edit: '2. Register',
    admit: '3. Admit',
    done: 'Done',
  };

  return (
    <div className="tabs" role="list">
      {order.map((value) => (
        <span
          key={value}
          role="listitem"
          className={
            value === current || (current === 'edit' && value === 'register')
              ? 'badge'
              : 'badge retired'
          }
        >
          {labels[value]}
        </span>
      ))}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Step 1 - find them
// ---------------------------------------------------------------------------

type LookupResult = {
  found: boolean;
  patient?: PatientSummary | null;
  hasOpenAdmission: boolean;
};

function FindStep({
  canEdit,
  onExisting,
  onEditExisting,
  onNew,
}: {
  canEdit: boolean;
  onExisting: (patient: PatientSummary) => void;
  onEditExisting: (patient: PatientSummary) => void;
  onNew: (nic: string) => void;
}) {
  const [nic, setNic] = useState('');
  const [result, setResult] = useState<LookupResult | null>(null);

  // Checked here so a typo is caught before it becomes a second record for somebody who is
  // already registered - which is the one thing this step exists to prevent.
  const nicError = nicProblem(nic);

  const lookup = useMutation({
    ...lookupPatientMutation(),
    // A miss comes back as 200 with found: false. It is a normal answer, not an error, so
    // there is nothing to toast and nothing for the interceptor to catch.
    onSuccess: (data) =>
      setResult({
        found: data.found,
        patient: data.patient,
        hasOpenAdmission: data.has_open_admission ?? false,
      }),
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    setResult(null);
    lookup.mutate({ body: { nic: nic.trim() } });
  }

  return (
    <>
      <div className="card">
        <h2>Search by NIC</h2>
        <p className="muted" style={{ marginBottom: '0.9rem' }}>
          This is what stops a returning patient being given a second record.
        </p>

        <form onSubmit={submit}>
          <div className="row">
            <div className="field">
              <label htmlFor="lookup-nic">NIC</label>
              <input
                id="lookup-nic"
                value={nic}
                maxLength={20}
                aria-invalid={nicError !== null}
                onChange={(event) => setNic(event.target.value)}
                placeholder="199534501V"
                required
              />
              {nicError && <p className="field-error">{nicError}</p>}
            </div>
          </div>

          <button
            type="submit"
            disabled={lookup.isPending || nic.trim().length === 0 || nicError !== null}
          >
            {lookup.isPending ? 'Searching...' : 'Search'}
          </button>
        </form>
      </div>

      {result?.found && result.patient && (
        <div className="card">
          <h2>Existing patient</h2>
          <PatientCard patient={result.patient} />

          {result.hasOpenAdmission ? (
            // No admit button at all. The server refuses this with cl_pat_006, and offering
            // a control that always fails is worse than not offering one.
            <p className="stub-note" style={{ marginTop: '0.9rem' }}>
              <strong>This patient is already admitted.</strong> A patient cannot hold two open
              admissions. Open their current visit on the patients board instead.
            </p>
          ) : (
            <button
              type="button"
              style={{ marginTop: '0.9rem' }}
              onClick={() => onExisting(result.patient as PatientSummary)}
            >
              Admit this patient
            </button>
          )}

          {/* Offered whether or not they can be admitted. A wrong date of birth on somebody
              already in a bed is still wrong, and this screen is where the desk has just
              noticed it. */}
          {canEdit && (
            <p style={{ marginTop: '0.6rem' }}>
              <button
                type="button"
                className="secondary"
                onClick={() => onEditExisting(result.patient as PatientSummary)}
              >
                Edit patient details
              </button>
            </p>
          )}
        </div>
      )}

      {result && !result.found && (
        <div className="card">
          <h2>No record for that NIC</h2>
          <p className="muted">
            No patient is registered under <strong>{nic.trim()}</strong>. Register them as a new
            patient — the NIC carries over to the form.
          </p>
          <button
            type="button"
            style={{ marginTop: '0.9rem' }}
            onClick={() => onNew(nic.trim())}
          >
            Register a new patient
          </button>
        </div>
      )}

      <div className="card">
        <h2>Patient has no NIC</h2>
        <p className="muted">
          An unconscious or unidentified arrival is registered with no NIC and no phone. The
          system allocates a temporary reference, such as <code>UNKNOWN-2026-0001</code>, so the
          patient can be admitted now and identified later. Adding a NIC afterwards does not
          clear the reference, which keeps the record traceable.
        </p>
        <button
          type="button"
          className="secondary"
          style={{ marginTop: '0.9rem' }}
          onClick={() => onNew('')}
        >
          Register an unidentified arrival
        </button>
      </div>
    </>
  );
}

// ---------------------------------------------------------------------------
// Step 2 - register, and its detour: edit
// ---------------------------------------------------------------------------
//
// Both render the same eight boxes from intake-form.tsx. They differ in the verb and in one
// fact: registering invents a record, editing corrects one that already exists.

function RegisterStep({
  nic,
  onBack,
  onRegistered,
}: {
  nic: string;
  onBack: () => void;
  onRegistered: (patient: Patient) => void;
}) {
  const queryClient = useQueryClient();
  const unidentified = nic.length === 0;

  const form = usePatientForm(emptyPatientForm(unidentified));
  const problems = patientFormProblems(form.value, !unidentified);

  const register = useMutation({
    ...createPatientMutation(),
    onSuccess: (created) => {
      // The patient ID, not the NIC, because it is the thing that has to be written down
      // now: every other part of the hospital asks for it and nowhere else shows it yet.
      toast.success(`${created.full_name} registered. Patient ID ${created.patient_code}.`);

      queryClient.invalidateQueries({
        predicate: (query) =>
          (query.queryKey[0] as { _id?: string } | undefined)?._id === 'listPatients',
      });

      onRegistered(created);
    },
  });

  return (
    <div className="card">
      <h2>Register a new patient</h2>

      {unidentified ? (
        <p className="muted" style={{ marginBottom: '0.9rem' }}>
          No NIC and no phone, so a temporary reference is allocated. Everything except the name
          and gender can be filled in later.
        </p>
      ) : (
        <p className="muted" style={{ marginBottom: '0.9rem' }}>
          NIC <strong>{nic}</strong>, carried over from the search. Everything except the
          emergency contact is required while the patient is here to answer.
        </p>
      )}

      {/* Said before the button, not after it. "Start over" clears these boxes and nothing
          else, so somebody who expects it to undo the registration is in for a surprise the
          next time they look the patient up. */}
      <p className="hint" style={{ marginBottom: '0.9rem' }}>
        Registering saves the record. Anything wrong can still be corrected at the next step.
        &ldquo;Start over&rdquo; only clears the form — it does not undo a registration.
      </p>

      <form
        onSubmit={onSubmit(() =>
          register.mutate({
            body: patientFormBody(form.value, unidentified ? null : nic),
          }),
        )}
      >
        <PatientFields
          value={form.value}
          set={form.set}
          idPrefix="reg"
          identified={!unidentified}
        />

        <div className="row">
          <button type="submit" disabled={register.isPending || problems.blocked}>
            {register.isPending ? 'Registering...' : 'Register and continue'}
          </button>
          <button type="button" className="secondary" onClick={onBack}>
            Start over
          </button>
        </div>
      </form>
    </div>
  );
}

/**
 * Correcting a record that already exists.
 *
 * The NIC is not on this form. Changing who a record IS, rather than what it says, is how one
 * person's history ends up on another person's record — so a wrong NIC is a new registration
 * and a merge, not a text box. The rest is fair game: names get misheard and phone numbers get
 * mistyped, constantly.
 */
function EditStep({
  patientId,
  onBack,
  onSaved,
}: {
  patientId: string;
  onBack: () => void;
  onSaved: (patient: Patient) => void;
}) {
  const queryClient = useQueryClient();

  // Read back rather than reused from the lookup, because the lookup returns a PatientSummary -
  // five fields - and this form has eight. Editing from the summary would blank the address and
  // both emergency contact fields on every save, which is the worst kind of bug: it looks like
  // it worked.
  const existing = useQuery(getPatientOptions({ path: { id: patientId } }));

  const form = usePatientForm(emptyPatientForm(false));

  // A record with a NIC belongs to somebody who was able to give one, so the same fields are
  // required here as at registration. A record still on a temporary reference is an arrival
  // nobody has identified yet, and this form is exactly where their details get filled in one
  // at a time as they are learned - demanding all of them would shut that door.
  const identified = (existing.data?.nic ?? null) !== null;
  const problems = patientFormProblems(form.value, identified);

  // Filled in once the record arrives. Keyed off the fetched data, not a mount, because the
  // query is not resolved on the first render.
  const [loadedId, setLoadedId] = useState<string | null>(null);

  if (existing.data && loadedId !== existing.data.id) {
    setLoadedId(existing.data.id);
    form.replace(patientFormFrom(existing.data));
  }

  const save = useMutation({
    ...updatePatientMutation(),
    onSuccess: (saved) => {
      toast.success(`${saved.full_name} updated.`);

      queryClient.invalidateQueries({
        predicate: (query) => {
          const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;

          return id === 'listPatients' || id === 'getPatient' || id === 'listPatientWorklist';
        },
      });

      onSaved(saved);
    },
  });

  if (existing.isLoading) {
    return (
      <div className="card">
        <p className="empty">Loading patient details…</p>
      </div>
    );
  }

  if (existing.isError || !existing.data) {
    return (
      <div className="card">
        <div className="empty">
          <p>Could not load the patient's details.</p>
          <button type="button" className="secondary" onClick={() => void existing.refetch()}>
            Try again
          </button>
        </div>
      </div>
    );
  }

  const patient = existing.data;

  return (
    <div className="card">
      <h2>Edit patient details</h2>
      <p className="muted" style={{ marginBottom: '0.9rem' }}>
        Patient ID <code>{patient.patient_code}</code>
        {patient.nic ? (
          <>
            {' '}
            · NIC <strong>{patient.nic}</strong>
          </>
        ) : (
          <>
            {' '}
            · Reference <strong>{patient.temp_reference}</strong>
          </>
        )}
      </p>

      <p className="hint" style={{ marginBottom: '0.9rem' }}>
        The NIC cannot be changed here. Changing which person a record identifies is how one
        patient&rsquo;s history ends up on another patient&rsquo;s record.
      </p>

      <form
        onSubmit={onSubmit(() =>
          save.mutate({
            path: { id: patientId },

            // The NIC goes back exactly as it came. Omitting it would read as "no NIC" and the
            // server would treat this as an unidentified arrival.
            body: patientFormBody(form.value, patient.nic ?? null),
          }),
        )}
      >
        <PatientFields
          value={form.value}
          set={form.set}
          idPrefix="edit"
          identified={identified}
        />

        <div className="row">
          <button type="submit" disabled={save.isPending || problems.blocked}>
            {save.isPending ? 'Saving...' : 'Save and go back'}
          </button>
          <button type="button" className="secondary" onClick={onBack}>
            Cancel
          </button>
        </div>
      </form>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Step 3 - admit
// ---------------------------------------------------------------------------

function AdmitStep({
  patient,
  staffId,
  canEdit,
  onEdit,
  onBack,
  onAdmitted,
}: {
  patient: Patient | PatientSummary;
  staffId: string;
  canEdit: boolean;
  onEdit: () => void;
  onBack: () => void;
  onAdmitted: (admission: Admission) => void;
}) {
  const queryClient = useQueryClient();

  const [category, setCategory] = useState<AdmissionCategory>('inpatient');
  const [urgency, setUrgency] = useState<AdmissionUrgency>('routine');
  const [isInfectious, setIsInfectious] = useState(false);

  const admit = useMutation({
    ...createAdmissionMutation(),
    onSuccess: (created) => {
      toast.success(`${patient.full_name} admitted.`);

      queryClient.invalidateQueries({
        predicate: (query) =>
          // The patients board as well: a walk-in admitted here is a new row on it.
          ['listAdmissions', 'listPatientWorklist'].includes(
            (query.queryKey[0] as { _id?: string } | undefined)?._id ?? '',
          ),
      });

      onAdmitted(created);
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    admit.mutate({
      body: {
        patient_id: patient.id,
        // This screen is the walk-in desk. An ambulance arrival is Emergency's path and
        // carries a dispatch id; a booked visit arrives through check-in. Neither is this.
        source: 'walk_in',
        admission_category: category,
        // The recorded proof that a human chose the care level. It is you, because you are
        // the one filling this in - there is no code path that lets an agent supply it.
        category_set_by_staff_id: staffId,
        urgency,
        is_infectious: isInfectious,
      },
    });
  }

  return (
    <div className="card">
      <h2>Admit</h2>

      {/* Read this before the table, not after it. This is the last point at which a typo is
          cheap: after admitting, the name is on the wristband, the bed and the bill. */}
      <p className="muted" style={{ marginBottom: '0.6rem' }}>
        <strong>Check these details before admitting.</strong> Anything wrong here follows the
        patient onto their wristband, their bed and their bill.
      </p>

      <PatientCard patient={patient} />

      {canEdit && (
        <p style={{ marginTop: '0.6rem' }}>
          {/* Not `secondary`, and directly under the table it edits. Phrased as a question and
              greyed out, this read as decoration and people pressed "Start over" instead -
              which does not undo a registration and never did. */}
          <button type="button" onClick={onEdit}>
            Edit patient details
          </button>
        </p>
      )}

      <form onSubmit={submit} style={{ marginTop: '0.9rem' }}>
        <div className="row">
          <div className="field">
            <label htmlFor="admit-category">Care level</label>
            <select
              id="admit-category"
              value={category}
              onChange={(event) => setCategory(event.target.value as AdmissionCategory)}
            >
              {admissionCategories.map((value) => (
                <option key={value} value={value}>
                  {admissionCategoryLabels[value]}
                </option>
              ))}
            </select>
            <p className="hint">
              {admissionCategoryHints[category]} This is your decision and is recorded against
              your name. The bed agent reads it and never sets it.
            </p>
          </div>
          <div className="field">
            <label htmlFor="admit-urgency">Urgency</label>
            <select
              id="admit-urgency"
              value={urgency}
              onChange={(event) => setUrgency(event.target.value as AdmissionUrgency)}
            >
              {admissionUrgencies.map((value) => (
                <option key={value} value={value}>
                  {admissionUrgencyLabels[value]}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="field">
          <label htmlFor="admit-infectious">
            <input
              id="admit-infectious"
              type="checkbox"
              checked={isInfectious}
              onChange={(event) => setIsInfectious(event.target.checked)}
            />{' '}
            Needs isolation
          </label>
          <p className="hint">Forces an isolation-capable bed when the bed agent runs.</p>
        </div>

        <div className="row">
          <button type="submit" disabled={admit.isPending}>
            {admit.isPending ? 'Admitting...' : 'Admit patient'}
          </button>
          <button type="button" className="secondary" onClick={onBack}>
            Start over
          </button>
        </div>

        {/* Said out loud, because the button used to say "Start over" and that is exactly what
            somebody presses when they spot a wrong name. It abandons the ADMISSION. The
            patient is registered and stays registered - there is no undo for that, which is
            why the edit button above exists. */}
        <p className="hint">
          &ldquo;Start over&rdquo; abandons this admission and returns to the search.
          <strong> It does not delete the patient</strong> — they are already registered. Use
          <strong> Edit patient details</strong> above to correct something.
        </p>
      </form>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Done
// ---------------------------------------------------------------------------

function DoneStep({ admission, onAnother }: { admission: Admission; onAnother: () => void }) {
  return (
    <div className="card">
      <h2>Admitted</h2>
      <p className="muted">
        {admission.patient?.full_name ?? 'The patient'} is admitted and awaiting a bed.
      </p>

      {/* Repeated here on purpose. This is the screen the desk is looking at while writing
          the wristband, and the ID is what every other component will ask for afterwards. */}
      {admission.patient && (
        <p>
          Patient ID <code>{admission.patient.patient_code}</code> — record this on the
          wristband. Equipment and the labs identify the patient by it.
        </p>
      )}

      {admission.missing_fields.length > 0 && (
        <>
          <p style={{ marginTop: '0.9rem' }}>
            <strong>Details still missing:</strong>
          </p>
          <ul>
            {admission.missing_fields.map((field) => (
              <li key={field}>{detailFieldLabel(field)}</li>
            ))}
          </ul>
          <p className="muted">
            Missing details do not block the admission. This is a list to follow up, not a gate.
          </p>
        </>
      )}

      <button type="button" style={{ marginTop: '0.9rem' }} onClick={onAnother}>
        Register another patient
      </button>
    </div>
  );
}

function PatientCard({ patient }: { patient: Patient | PatientSummary }) {
  const reference = patientIdentifier(patient);

  return (
    <table>
      <tbody>
        <tr>
          <th scope="row">Patient ID</th>
          <td>
            <code>{patient.patient_code}</code>
          </td>
        </tr>
        <tr>
          <th scope="row">Name</th>
          <td>
            <strong>{patient.full_name}</strong>
          </td>
        </tr>
        <tr>
          <th scope="row">{patient.nic ? 'NIC' : 'Reference'}</th>
          <td>{reference ?? <span className="muted">None yet</span>}</td>
        </tr>
        <tr>
          <th scope="row">Gender</th>
          <td>{genderLabels[patient.gender]}</td>
        </tr>
        <tr>
          <th scope="row">Date of birth</th>
          <td>{patient.date_of_birth ?? <span className="muted">Not recorded</span>}</td>
        </tr>

        {/* The other three, when we have them. A desk asked to "check this is right" against a
            table showing five of the eight fields they just typed cannot actually check it -
            a mistyped phone number would never appear.

            A PatientSummary carries only the five above, which is why this is conditional
            rather than three more rows: coming from the lookup there is nothing to show, and
            three rows reading "Not recorded" would look like missing data rather than an
            unread field. */}
        {'phone' in patient && (
          <>
            <tr>
              <th scope="row">Phone</th>
              <td>{patient.phone ?? <span className="muted">Not recorded</span>}</td>
            </tr>
            <tr>
              <th scope="row">Address</th>
              <td>{patient.address ?? <span className="muted">Not recorded</span>}</td>
            </tr>
            <tr>
              <th scope="row">Emergency contact</th>
              <td>
                {patient.emergency_contact_name ? (
                  <>
                    {patient.emergency_contact_name}
                    {patient.emergency_contact_phone ? ` · ${patient.emergency_contact_phone}` : ''}
                  </>
                ) : (
                  <span className="muted">Not recorded</span>
                )}
              </td>
            </tr>
          </>
        )}
      </tbody>
    </table>
  );
}
