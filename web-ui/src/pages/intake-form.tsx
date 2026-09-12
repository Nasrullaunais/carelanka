import { useState } from 'react';
import type { FormEvent } from 'react';
import type { Gender, Patient } from '../services/api/generated';
import {
  birthYears,
  dateOfBirthProblem,
  daysInMonth,
  fieldLimits,
  monthNames,
  phoneProblem,
  toIsoDate,
} from '../types/identifiers';
import { genderLabels, genders } from '../types/patients';

// The patient details form, written once and used twice: to register somebody, and to correct
// what was typed.
//
// It lives in its own file because the two callers are the same eight boxes with the same eight
// rules and a different verb. Two copies is how a validation rule gets fixed on the register
// form and left wrong on the edit form, and nobody notices until a bad phone number is already
// in the database.

export type PatientFormValue = {
  fullName: string;
  gender: Gender;
  phone: string;
  address: string;
  contactName: string;
  contactPhone: string;

  // Three pickers rather than one <input type="date">. The native control opens on this month,
  // and a patient born in 1997 is a long way back from there - which is how a desk ends up
  // leaving date of birth blank on every record.
  birthYear: number | null;
  birthMonth: number | null;
  birthDay: number | null;
};

export function emptyPatientForm(unidentified: boolean): PatientFormValue {
  return {
    fullName: unidentified ? 'Unidentified patient' : '',
    gender: unidentified ? 'unknown' : 'male',
    phone: '',
    address: '',
    contactName: '',
    contactPhone: '',
    birthYear: null,
    birthMonth: null,
    birthDay: null,
  };
}

/** An existing record, unpacked into the boxes. Used to prefill the edit form. */
export function patientFormFrom(patient: Patient): PatientFormValue {
  const [year, month, day] = (patient.date_of_birth ?? '').split('-');

  return {
    fullName: patient.full_name,
    gender: patient.gender,
    phone: patient.phone ?? '',
    address: patient.address ?? '',
    contactName: patient.emergency_contact_name ?? '',
    contactPhone: patient.emergency_contact_phone ?? '',
    birthYear: year ? Number(year) : null,
    birthMonth: month ? Number(month) : null,
    birthDay: day ? Number(day) : null,
  };
}

/**
 * What is wrong with the form right now, or nothing.
 *
 * Told at the field, not on submit. The server checks all of this too - a browser is not a
 * boundary - but a form that accepts what you typed and then fails on submit makes you hunt for
 * which of eight boxes was wrong.
 *
 * `identified` is what decides whether a blank box is an error. Somebody with a NIC is standing
 * at the desk answering questions, so leaving their address out is an omission and the desk
 * should be made to fix it now rather than leave it as paperwork to chase. An unconscious
 * arrival cannot answer any of them, and a form that will not submit without a date of birth
 * for a patient nobody can name is a form that stops them being admitted at all.
 *
 * Date of birth is the one that earns this on its own: the bed board reads it to decide whether
 * a children's ward is offered, so a blank one quietly costs a child the right ward.
 *
 * The emergency contact stays optional either way. Plenty of people genuinely arrive alone.
 */
export function patientFormProblems(value: PatientFormValue, identified = false) {
  const dateOfBirth = toIsoDate(value.birthYear, value.birthMonth, value.birthDay);

  const phone = phoneProblem(value.phone);
  const contactPhone = phoneProblem(value.contactPhone);
  const dateOfBirthError = dateOfBirthProblem(dateOfBirth);

  // A part-filled date is not an error, it is an unfinished one. Saying "that is not a date" to
  // somebody who has picked the year and is reaching for the month is just rude.
  const dateIncomplete =
    (value.birthYear !== null || value.birthMonth !== null || value.birthDay !== null) &&
    dateOfBirth === '';

  const missing = {
    phone: identified && value.phone.trim().length === 0,
    address: identified && value.address.trim().length === 0,
    dateOfBirth: identified && dateOfBirth === '' && !dateIncomplete,
  };

  const blocked =
    value.fullName.trim().length === 0 ||
    phone !== null ||
    contactPhone !== null ||
    dateOfBirthError !== null ||
    dateIncomplete ||
    missing.phone ||
    missing.address ||
    missing.dateOfBirth;

  return {
    isoDate: dateOfBirth,
    phone,
    contactPhone,
    dateOfBirth: dateOfBirthError,
    dateIncomplete,
    missing,
    blocked,
  };
}

/**
 * The form as the API takes it.
 *
 * Blank optional fields go as null, not "". The server computes missing_fields off the patient
 * row, so an empty string would count as filled in and the desk would never be told to chase it.
 */
export function patientFormBody(value: PatientFormValue, nic: string | null) {
  const orNull = (text: string) => (text.trim().length > 0 ? text.trim() : null);

  return {
    full_name: value.fullName.trim(),
    nic,
    gender: value.gender,
    date_of_birth: orNull(toIsoDate(value.birthYear, value.birthMonth, value.birthDay)),
    phone: orNull(value.phone),
    address: orNull(value.address),
    emergency_contact_name: orNull(value.contactName),
    emergency_contact_phone: orNull(value.contactPhone),
  };
}

/** Keeps the boxes, and gives back one setter so a caller changes one field at a time. */
export function usePatientForm(initial: PatientFormValue) {
  const [value, setValue] = useState(initial);

  function set<K extends keyof PatientFormValue>(key: K, next: PatientFormValue[K]) {
    setValue((current) => ({ ...current, [key]: next }));
  }

  return { value, set, replace: setValue };
}

/**
 * How much room is left in a field, shown only once it starts to matter.
 *
 * A counter sitting under every box from the moment the form loads is noise; one that appears
 * at three quarters full is a warning. Either way the input's own maxLength is what stops the
 * typing - this only explains why it stopped.
 */
export function Counter({ value, limit }: { value: string; limit: number }) {
  if (value.length < limit * 0.75) {
    return null;
  }

  const left = limit - value.length;

  return (
    <p className={left === 0 ? 'field-error' : 'hint'}>
      {left === 0 ? `That is the limit - ${limit} characters.` : `${left} characters left.`}
    </p>
  );
}

export function PatientFields({
  value,
  set,
  idPrefix,
  identified = false,
}: {
  value: PatientFormValue;
  set: <K extends keyof PatientFormValue>(key: K, next: PatientFormValue[K]) => void;
  /** Register and edit can both be mounted in one session, so the ids must not collide. */
  idPrefix: string;
  /**
   * Whether this patient can answer questions about themselves. Everything except the emergency
   * contact is required when they can. See {@link patientFormProblems}.
   */
  identified?: boolean;
}) {
  const problems = patientFormProblems(value, identified);

  return (
    <>
      <div className="row">
        <div className="field">
          <label htmlFor={`${idPrefix}-name`}>Full name</label>
          <input
            id={`${idPrefix}-name`}
            value={value.fullName}
            maxLength={fieldLimits.fullName}
            onChange={(event) => set('fullName', event.target.value)}
            required
          />
          <Counter value={value.fullName} limit={fieldLimits.fullName} />
        </div>
        <div className="field">
          <label htmlFor={`${idPrefix}-gender`}>Gender</label>
          <select
            id={`${idPrefix}-gender`}
            value={value.gender}
            onChange={(event) => set('gender', event.target.value as Gender)}
          >
            {genders.map((option) => (
              <option key={option} value={option}>
                {genderLabels[option]}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="row">
        <div className="field">
          <label htmlFor={`${idPrefix}-dob-day`}>Date of birth</label>
          <div className="row" style={{ gap: '0.4rem' }}>
            <select
              id={`${idPrefix}-dob-day`}
              aria-label="Day of birth"
              value={value.birthDay ?? ''}
              onChange={(event) =>
                set('birthDay', event.target.value === '' ? null : Number(event.target.value))
              }
            >
              <option value="">Day</option>
              {Array.from(
                { length: daysInMonth(value.birthYear, value.birthMonth) },
                (_, index) => index + 1,
              ).map((day) => (
                <option key={day} value={day}>
                  {day}
                </option>
              ))}
            </select>
            <select
              aria-label="Month of birth"
              value={value.birthMonth ?? ''}
              onChange={(event) =>
                set('birthMonth', event.target.value === '' ? null : Number(event.target.value))
              }
            >
              <option value="">Month</option>
              {monthNames.map((name, index) => (
                <option key={name} value={index + 1}>
                  {name}
                </option>
              ))}
            </select>
            <select
              aria-label="Year of birth"
              value={value.birthYear ?? ''}
              onChange={(event) =>
                set('birthYear', event.target.value === '' ? null : Number(event.target.value))
              }
            >
              <option value="">Year</option>
              {birthYears().map((year) => (
                <option key={year} value={year}>
                  {year}
                </option>
              ))}
            </select>
          </div>
          {problems.dateOfBirth && <p className="field-error">{problems.dateOfBirth}</p>}
          {problems.dateIncomplete && !problems.dateOfBirth && (
            <p className="hint">
              {identified ? 'Pick all three.' : 'Pick all three, or leave all three blank.'}
            </p>
          )}
          {problems.missing.dateOfBirth && (
            <p className="field-error">
              Required. The ward a patient can be given depends on their age.
            </p>
          )}
        </div>
      </div>

      <div className="row">
        <div className="field">
          <label htmlFor={`${idPrefix}-phone`}>Phone</label>
          <input
            id={`${idPrefix}-phone`}
            value={value.phone}
            inputMode="tel"
            maxLength={20}
            placeholder="0771234567"
            aria-invalid={problems.phone !== null || problems.missing.phone}
            required={identified}
            onChange={(event) => set('phone', event.target.value)}
          />
          {problems.phone && <p className="field-error">{problems.phone}</p>}
          {problems.missing.phone && <p className="field-error">Required.</p>}
        </div>
        <div className="field">
          <label htmlFor={`${idPrefix}-address`}>Address</label>
          <input
            id={`${idPrefix}-address`}
            value={value.address}
            maxLength={fieldLimits.address}
            aria-invalid={problems.missing.address}
            required={identified}
            onChange={(event) => set('address', event.target.value)}
          />
          {problems.missing.address && <p className="field-error">Required.</p>}
          <Counter value={value.address} limit={fieldLimits.address} />
        </div>
      </div>

      <div className="row">
        <div className="field">
          {/* "Emergency contact" sitting next to "Emergency contact phone" reads as though the
              first one also wants a number. Say what goes in the box. */}
          <label htmlFor={`${idPrefix}-contact-name`}>
            Who to ring in an emergency {identified && <span className="muted">(optional)</span>}
          </label>
          <input
            id={`${idPrefix}-contact-name`}
            value={value.contactName}
            maxLength={fieldLimits.contactName}
            placeholder="Nilanthi Gunawardena"
            onChange={(event) => set('contactName', event.target.value)}
          />
          <p className="hint">Their name.</p>
        </div>
        <div className="field">
          <label htmlFor={`${idPrefix}-contact-phone`}>
            Their phone number {identified && <span className="muted">(optional)</span>}
          </label>
          <input
            id={`${idPrefix}-contact-phone`}
            value={value.contactPhone}
            inputMode="tel"
            maxLength={20}
            placeholder="0779876543"
            aria-invalid={problems.contactPhone !== null}
            onChange={(event) => set('contactPhone', event.target.value)}
          />
          {problems.contactPhone && <p className="field-error">{problems.contactPhone}</p>}
        </div>
      </div>
    </>
  );
}

/** Submit handler wrapper, so neither caller has to remember preventDefault. */
export function onSubmit(handler: () => void) {
  return (event: FormEvent) => {
    event.preventDefault();
    handler();
  };
}
