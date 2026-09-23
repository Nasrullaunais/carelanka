import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
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
  Patient,
  PatientSummary,
} from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { fieldLimits, nicProblem } from '../types/identifiers';
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
  detailFieldLabel,
  genderLabels,
  patientIdentifier,
} from '../types/patients';

type Step = 'find' | 'level' | 'register' | 'emergency-register' | 'edit' | 'admit' | 'done';

export function IntakePage() {
  const session = useSession();
  const role = session?.principal.role;
  const navigate = useNavigate();

  const [step, setStep] = useState<Step>('find');
  const [patient, setPatient] = useState<Patient | PatientSummary | null>(null);
  const [knownNic, setKnownNic] = useState('');
  const [pendingCategory, setPendingCategory] = useState<AdmissionCategory>('general');
  const [admission, setAdmission] = useState<Admission | null>(null);

  const [editReturn, setEditReturn] = useState<'find' | 'admit'>('admit');

  function leaveEdit() {
    if (editReturn === 'find') {
      restart();
      return;
    }

    setStep('admit');
  }

  function restart() {
    setStep('find');
    setPatient(null);
    setKnownNic('');
    setPendingCategory('general');
    setAdmission(null);
  }

  function afterAdmission(created: Admission) {
    if (created.admission_category === 'emergency') {
      navigate(`/patients?assignBed=${created.id}`);
      return;
    }

    setAdmission(created);
    setStep('done');
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
            setStep('level');
          }}
        />
      )}

      {step === 'level' && (
        <LevelStep
          onBack={restart}
          onSelect={(category) => {
            if (category === 'emergency') {
              setPendingCategory(category);
              setStep('emergency-register');
              return;
            }

            setPendingCategory(category);
            setStep('register');
          }}
        />
      )}

      {step === 'register' && (
        <RegisterStep
          nic={knownNic}
          category={pendingCategory}
          onBack={() => setStep('level')}
          onRegistered={(created) => {
            setPatient(created);
            setStep('admit');
          }}
        />
      )}

      {step === 'emergency-register' && (
        <EmergencyRegisterStep
          nic={knownNic}
          staffId={session?.principal.id ?? ''}
          onBack={() => setStep('level')}
          onAdmitted={afterAdmission}
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
          initialCategory={pendingCategory}
          onEdit={() => {
            setEditReturn('admit');
            setStep('edit');
          }}
          onBack={restart}
          onAdmitted={afterAdmission}
        />
      )}

      {step === 'done' && admission && <DoneStep admission={admission} onAnother={restart} />}
    </>
  );
}

function Steps({ current }: { current: Step }) {
  const order: Step[] = ['find', 'level', 'register', 'admit'];
  const labels: Record<Step, string> = {
    find: '1. Search',
    level: '2. Care level',
    register: '3. Register',
    'emergency-register': '3. Brief record',
    edit: '3. Register',
    admit: '4. Admit',
    done: 'Done',
  };

  const active =
    current === 'emergency-register'
      ? 'register'
      : current === 'edit'
        ? 'register'
        : current;

  return (
    <div className="tabs" role="list">
      {order.map((value) => (
        <span
          key={value}
          role="listitem"
          className={value === active ? 'badge' : 'badge retired'}
        >
          {value === 'register' && current === 'emergency-register'
            ? labels['emergency-register']
            : labels[value]}
        </span>
      ))}
    </div>
  );
}

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

  const nicError = nicProblem(nic);

  const lookup = useMutation({
    ...lookupPatientMutation(),
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

function LevelStep({
  onBack,
  onSelect,
}: {
  onBack: () => void;
  onSelect: (category: AdmissionCategory) => void;
}) {
  return (
    <div className="card">
      <h2>Care level</h2>
      <p className="muted" style={{ marginBottom: '0.9rem' }}>
        Choose before registering — an emergency skips the full form.
      </p>

      <div className="row" style={{ flexWrap: 'wrap', gap: '0.6rem' }}>
        {admissionCategories.map((value) => (
          <button
            key={value}
            type="button"
            className={value === 'emergency' ? undefined : 'secondary'}
            style={{ flex: '1 1 auto', textAlign: 'left' }}
            onClick={() => onSelect(value)}
          >
            <strong>{admissionCategoryLabels[value]}</strong>
            <br />
            <span className="hint">{admissionCategoryHints[value]}</span>
          </button>
        ))}
      </div>

      <button type="button" className="secondary" style={{ marginTop: '0.9rem' }} onClick={onBack}>
        Start over
      </button>
    </div>
  );
}

function EmergencyRegisterStep({
  nic,
  staffId,
  onBack,
  onAdmitted,
}: {
  nic: string;
  staffId: string;
  onBack: () => void;
  onAdmitted: (admission: Admission) => void;
}) {
  const queryClient = useQueryClient();
  const form = usePatientForm(emptyPatientForm(nic.length === 0, 'unknown'));
  const problems = patientFormProblems(form.value, false);
  const [isInfectious, setIsInfectious] = useState(false);

  const register = useMutation({
    ...createPatientMutation(),
  });

  const admit = useMutation({
    ...createAdmissionMutation(),
    onSuccess: (created) => {
      queryClient.invalidateQueries({
        predicate: (query) =>
          ['listAdmissions', 'listPatientWorklist'].includes(
            (query.queryKey[0] as { _id?: string } | undefined)?._id ?? '',
          ),
      });

      onAdmitted(created);
    },
  });

  async function submit(event: FormEvent) {
    event.preventDefault();

    const patient = await register.mutateAsync({
      body: patientFormBody(form.value, nic.length === 0 ? null : nic),
    });

    toast.success(`${patient.full_name} registered. Patient ID ${patient.patient_code}.`);

    queryClient.invalidateQueries({
      predicate: (query) =>
        (query.queryKey[0] as { _id?: string } | undefined)?._id === 'listPatients',
    });

    admit.mutate({
      body: {
        patient_id: patient.id,
        source: 'walk_in',
        admission_category: 'emergency',
        category_set_by_staff_id: staffId,
        urgency: 'routine',
        is_infectious: isInfectious,
      },
    });
  }

  const pending = register.isPending || admit.isPending;

  return (
    <div className="card">
      <h2>Emergency — brief record</h2>
      <p className="muted" style={{ marginBottom: '0.9rem' }}>
        Just the name, to admit and pick a bed now. Gender, date of birth, phone, address and
        the emergency/guardian contact are all left for a nurse or reception to fill in later
        from <strong>Edit patient details</strong> on this patient&rsquo;s record.
      </p>

      <p className="hint" style={{ marginBottom: '0.9rem' }}>
        {nic.length > 0 ? (
          <>
            NIC <strong>{nic}</strong>, carried over from the search.
          </>
        ) : (
          <>No NIC — a temporary reference is allocated once admitted.</>
        )}
      </p>

      <form onSubmit={(event) => void submit(event)}>
        <div className="field">
          <label htmlFor="emergency-name">Full name</label>
          <input
            id="emergency-name"
            value={form.value.fullName}
            maxLength={fieldLimits.fullName}
            onChange={(event) => form.set('fullName', event.target.value)}
            required
            autoFocus
          />
        </div>

        <div className="field">
          <label htmlFor="emergency-infectious">
            <input
              id="emergency-infectious"
              type="checkbox"
              checked={isInfectious}
              onChange={(event) => setIsInfectious(event.target.checked)}
            />{' '}
            Needs isolation
          </label>
        </div>

        <div className="row">
          <button type="submit" disabled={pending || problems.blocked}>
            {pending ? 'Admitting...' : 'Admit and pick a bed'}
          </button>
          <button type="button" className="secondary" onClick={onBack}>
            Back
          </button>
        </div>
      </form>
    </div>
  );
}

function RegisterStep({
  nic,
  category,
  onBack,
  onRegistered,
}: {
  nic: string;
  category: AdmissionCategory;
  onBack: () => void;
  onRegistered: (patient: Patient) => void;
}) {
  const queryClient = useQueryClient();
  const unidentified = nic.length === 0;
  const lockGenderTo = category === 'maternity' ? 'female' : undefined;

  const form = usePatientForm(emptyPatientForm(unidentified, lockGenderTo));
  const problems = patientFormProblems(form.value, !unidentified, unidentified ? '' : nic);

  const register = useMutation({
    ...createPatientMutation(),
    onSuccess: (created) => {
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
          lockGenderTo={lockGenderTo}
          allowUnknownGender={unidentified}
          nic={unidentified ? '' : nic}
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

  const existing = useQuery(getPatientOptions({ path: { id: patientId } }));

  const form = usePatientForm(emptyPatientForm(false));

  const identified = (existing.data?.nic ?? null) !== null;
  const problems = patientFormProblems(form.value, identified, existing.data?.nic ?? '');

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

            body: patientFormBody(form.value, patient.nic ?? null),
          }),
        )}
      >
        <PatientFields
          value={form.value}
          set={form.set}
          idPrefix="edit"
          identified={identified}
          allowUnknownGender={!identified}
          nic={patient.nic ?? ''}
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

function AdmitStep({
  patient,
  staffId,
  canEdit,
  initialCategory,
  onEdit,
  onBack,
  onAdmitted,
}: {
  patient: Patient | PatientSummary;
  staffId: string;
  canEdit: boolean;
  initialCategory?: AdmissionCategory;
  onEdit: () => void;
  onBack: () => void;
  onAdmitted: (admission: Admission) => void;
}) {
  const queryClient = useQueryClient();

  const categoryChosenEarlier = initialCategory !== undefined;
  const availableCategories =
    patient.gender === 'female'
      ? admissionCategories
      : admissionCategories.filter((value) => value !== 'maternity');

  const [chosenCategory, setChosenCategory] = useState<AdmissionCategory>(
    initialCategory ?? 'general',
  );
  const category = categoryChosenEarlier ? (initialCategory as AdmissionCategory) : chosenCategory;
  const [isInfectious, setIsInfectious] = useState(false);

  const admit = useMutation({
    ...createAdmissionMutation(),
    onSuccess: (created) => {
      toast.success(`${patient.full_name} admitted.`);

      queryClient.invalidateQueries({
        predicate: (query) =>
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
        source: 'walk_in',
        admission_category: category,
        category_set_by_staff_id: staffId,
        urgency: 'routine',
        is_infectious: isInfectious,
      },
    });
  }

  return (
    <div className="card">
      <h2>Admit</h2>

      <p className="muted" style={{ marginBottom: '0.6rem' }}>
        <strong>Check these details before admitting.</strong> Anything wrong here follows the
        patient onto their wristband, their bed and their bill.
      </p>

      <PatientCard patient={patient} />

      {canEdit && (
        <p style={{ marginTop: '0.6rem' }}>

          <button type="button" onClick={onEdit}>
            Edit patient details
          </button>
        </p>
      )}

      <form onSubmit={submit} style={{ marginTop: '0.9rem' }}>
        <div className="field">
          <label>Care level</label>
          {categoryChosenEarlier ? (
            <>
              <p>
                <strong>{admissionCategoryLabels[category]}</strong>
              </p>
              <p className="hint">
                {admissionCategoryHints[category]} Chosen on the previous step. To change it,
                use &ldquo;Start over&rdquo; below.
              </p>
            </>
          ) : (
            <>
              <select
                id="admit-category"
                value={category}
                onChange={(event) => setChosenCategory(event.target.value as AdmissionCategory)}
              >
                {availableCategories.map((value) => (
                  <option key={value} value={value}>
                    {admissionCategoryLabels[value]}
                  </option>
                ))}
              </select>
              <p className="hint">
                {admissionCategoryHints[category]} This is your decision and is recorded against
                your name.
              </p>
            </>
          )}
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
          <p className="hint">Forces an isolation-capable bed when a bed is assigned.</p>
        </div>

        <div className="row">
          <button type="submit" disabled={admit.isPending}>
            {admit.isPending ? 'Admitting...' : 'Admit patient'}
          </button>
          <button type="button" className="secondary" onClick={onBack}>
            Start over
          </button>
        </div>

        <p className="hint">
          &ldquo;Start over&rdquo; abandons this admission and returns to the search.
          <strong> It does not delete the patient</strong> — they are already registered. Use
          <strong> Edit patient details</strong> above to correct something.
        </p>
      </form>
    </div>
  );
}

function DoneStep({ admission, onAnother }: { admission: Admission; onAnother: () => void }) {
  return (
    <div className="card">
      <h2>Admitted</h2>
      <p className="muted">
        {admission.patient?.full_name ?? 'The patient'} is admitted and awaiting a bed.
      </p>

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
              <th scope="row">Emergency/guardian contact</th>
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
