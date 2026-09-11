import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  createAdmissionMutation,
  createPatientMutation,
  lookupPatientMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type {
  Admission,
  AdmissionCategory,
  AdmissionUrgency,
  Gender,
  Patient,
  PatientSummary,
} from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { canRegisterPatient } from '../types/permissions';
import {
  admissionCategories,
  admissionCategoryHints,
  admissionCategoryLabels,
  admissionUrgencies,
  admissionUrgencyLabels,
  detailFieldLabel,
  genderLabels,
  genders,
  patientIdentifier,
} from '../types/patients';

// Someone has walked up to the desk. Three steps, in this order on purpose:
//
//   find  ->  register (only if they are new)  ->  admit
//
// The lookup is first because a returning patient must keep one record with many admissions.
// A second record for the same person is the most damaging data problem in this component,
// and it stays invisible until somebody needs the history.

type Step = 'find' | 'register' | 'admit' | 'done';

export function IntakePage() {
  const session = useSession();
  const role = session?.principal.role;

  const [step, setStep] = useState<Step>('find');
  const [patient, setPatient] = useState<Patient | PatientSummary | null>(null);
  const [knownNic, setKnownNic] = useState('');
  const [admission, setAdmission] = useState<Admission | null>(null);

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
        Register someone who has arrived at the desk, then admit them. Look them up first: a
        returning patient keeps one record.
      </p>

      <Steps current={step} />

      {step === 'find' && (
        <FindStep
          onExisting={(found) => {
            setPatient(found);
            setStep('admit');
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

      {step === 'admit' && patient && (
        <AdmitStep
          patient={patient}
          staffId={session?.principal.id ?? ''}
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
    find: '1. Find them',
    register: '2. Register',
    admit: '3. Admit',
    done: 'Done',
  };

  return (
    <div className="tabs" role="list">
      {order.map((value) => (
        <span
          key={value}
          role="listitem"
          className={value === current ? 'badge' : 'badge retired'}
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
  onExisting,
  onNew,
}: {
  onExisting: (patient: PatientSummary) => void;
  onNew: (nic: string) => void;
}) {
  const [nic, setNic] = useState('');
  const [result, setResult] = useState<LookupResult | null>(null);

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
        <h2>Look them up</h2>
        <p className="muted" style={{ marginBottom: '0.9rem' }}>
          By NIC. This is the guard against a returning patient gaining a second identity.
        </p>

        <form onSubmit={submit}>
          <div className="row">
            <div className="field">
              <label htmlFor="lookup-nic">NIC</label>
              <input
                id="lookup-nic"
                value={nic}
                maxLength={20}
                onChange={(event) => setNic(event.target.value)}
                placeholder="199534501V"
                required
              />
            </div>
          </div>

          <button type="submit" disabled={lookup.isPending || nic.trim().length === 0}>
            {lookup.isPending ? 'Looking up...' : 'Look up'}
          </button>
        </form>
      </div>

      {result?.found && result.patient && (
        <div className="card">
          <h2>Already registered</h2>
          <PatientCard patient={result.patient} />

          {result.hasOpenAdmission ? (
            // No admit button at all. The server refuses this with cl_pat_006, and offering
            // a control that always fails is worse than not offering one.
            <p className="stub-note" style={{ marginTop: '0.9rem' }}>
              <strong>This patient is already in the hospital.</strong> They have an open
              admission, and one person cannot be admitted twice at once. Find their current
              visit on the admissions worklist rather than starting a second one.
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
        </div>
      )}

      {result && !result.found && (
        <div className="card">
          <h2>No record for that NIC</h2>
          <p className="muted">
            Nobody is registered under <strong>{nic.trim()}</strong>. Register them as a new
            patient; the NIC carries over to the form.
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
        <h2>No NIC?</h2>
        <p className="muted">
          An unconscious or unidentified arrival is registered with no NIC and no phone. The
          server allocates a temporary reference, such as <code>UNKNOWN-2026-0001</code>, so
          they can be admitted now and identified later. Adding a NIC afterwards never clears
          it, which is what keeps the paper trail honest.
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
// Step 2 - register
// ---------------------------------------------------------------------------

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

  const [fullName, setFullName] = useState(unidentified ? 'Unidentified patient' : '');
  const [gender, setGender] = useState<Gender>(unidentified ? 'unknown' : 'male');
  const [dateOfBirth, setDateOfBirth] = useState('');
  const [phone, setPhone] = useState('');
  const [address, setAddress] = useState('');
  const [contactName, setContactName] = useState('');
  const [contactPhone, setContactPhone] = useState('');

  const register = useMutation({
    ...createPatientMutation(),
    onSuccess: (created) => {
      const reference = patientIdentifier(created);
      toast.success(
        reference
          ? `${created.full_name} registered as ${reference}.`
          : `${created.full_name} registered.`,
      );

      queryClient.invalidateQueries({
        predicate: (query) =>
          (query.queryKey[0] as { _id?: string } | undefined)?._id === 'listPatients',
      });

      onRegistered(created);
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();

    // Blank optional fields go as null, not "". The server computes missing_fields off the
    // patient row, so an empty string would count as filled in and the desk would never be
    // told to chase it.
    const orNull = (value: string) => (value.trim().length > 0 ? value.trim() : null);

    register.mutate({
      body: {
        full_name: fullName.trim(),
        nic: unidentified ? null : nic,
        gender,
        date_of_birth: orNull(dateOfBirth),
        phone: orNull(phone),
        address: orNull(address),
        emergency_contact_name: orNull(contactName),
        emergency_contact_phone: orNull(contactPhone),
      },
    });
  }

  return (
    <div className="card">
      <h2>Register a new patient</h2>

      {unidentified ? (
        <p className="muted" style={{ marginBottom: '0.9rem' }}>
          No NIC and no phone, so the server allocates a temporary reference. Everything below
          except the name and gender can be filled in later.
        </p>
      ) : (
        <p className="muted" style={{ marginBottom: '0.9rem' }}>
          NIC <strong>{nic}</strong>, carried over from the lookup. Anything left blank shows
          up as outstanding paperwork on the admission, named rather than counted.
        </p>
      )}

      <form onSubmit={submit}>
        <div className="row">
          <div className="field">
            <label htmlFor="reg-name">Full name</label>
            <input
              id="reg-name"
              value={fullName}
              maxLength={200}
              onChange={(event) => setFullName(event.target.value)}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="reg-gender">Gender</label>
            <select
              id="reg-gender"
              value={gender}
              onChange={(event) => setGender(event.target.value as Gender)}
            >
              {genders.map((value) => (
                <option key={value} value={value}>
                  {genderLabels[value]}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="reg-dob">Date of birth</label>
            <input
              id="reg-dob"
              type="date"
              value={dateOfBirth}
              onChange={(event) => setDateOfBirth(event.target.value)}
            />
          </div>
        </div>

        <div className="row">
          <div className="field">
            <label htmlFor="reg-phone">Phone</label>
            <input
              id="reg-phone"
              value={phone}
              maxLength={20}
              onChange={(event) => setPhone(event.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="reg-address">Address</label>
            <input
              id="reg-address"
              value={address}
              maxLength={300}
              onChange={(event) => setAddress(event.target.value)}
            />
          </div>
        </div>

        <div className="row">
          <div className="field">
            <label htmlFor="reg-contact-name">Emergency contact</label>
            <input
              id="reg-contact-name"
              value={contactName}
              maxLength={200}
              onChange={(event) => setContactName(event.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="reg-contact-phone">Emergency contact phone</label>
            <input
              id="reg-contact-phone"
              value={contactPhone}
              maxLength={20}
              onChange={(event) => setContactPhone(event.target.value)}
            />
          </div>
        </div>

        <div className="row">
          <button type="submit" disabled={register.isPending || fullName.trim().length === 0}>
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

// ---------------------------------------------------------------------------
// Step 3 - admit
// ---------------------------------------------------------------------------

function AdmitStep({
  patient,
  staffId,
  onBack,
  onAdmitted,
}: {
  patient: Patient | PatientSummary;
  staffId: string;
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
      <PatientCard patient={patient} />

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
              {admissionCategoryHints[category]} You are choosing this and it is recorded
              against your name; the bed agent reasons from it and never sets it.
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
        {admission.patient?.full_name ?? 'The patient'} is admitted and waiting for a bed.
      </p>

      {admission.missing_fields.length > 0 && (
        <>
          <p style={{ marginTop: '0.9rem' }}>
            <strong>Paperwork still outstanding:</strong>
          </p>
          <ul>
            {admission.missing_fields.map((field) => (
              <li key={field}>{detailFieldLabel(field)}</li>
            ))}
          </ul>
          <p className="muted">
            Missing paperwork does not block the admission. A patient can be admitted with
            details outstanding; this is a list to chase, not a gate.
          </p>
        </>
      )}

      <button type="button" style={{ marginTop: '0.9rem' }} onClick={onAnother}>
        Register someone else
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
      </tbody>
    </table>
  );
}
