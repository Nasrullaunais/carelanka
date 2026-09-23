import { useState } from 'react';
import type { FormEvent } from 'react';
import type { Gender, Patient } from '../services/api/generated';
import {
  birthYears,
  dateOfBirthProblem,
  daysInMonth,
  fieldLimits,
  monthNames,
  nicBirthYearProblem,
  phoneProblem,
  toIsoDate,
} from '../types/identifiers';
import { genderLabels, selectableGenders } from '../types/patients';

export type PatientFormValue = {
  fullName: string;
  gender: Gender;
  phone: string;
  address: string;
  contactName: string;
  contactPhone: string;

  birthYear: number | null;
  birthMonth: number | null;
  birthDay: number | null;
};

export function emptyPatientForm(unidentified: boolean, forcedGender?: Gender): PatientFormValue {
  return {
    fullName: unidentified ? 'Unidentified patient' : '',
    gender: forcedGender ?? (unidentified ? 'unknown' : 'male'),
    phone: '',
    address: '',
    contactName: '',
    contactPhone: '',
    birthYear: null,
    birthMonth: null,
    birthDay: null,
  };
}

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

export function patientFormProblems(value: PatientFormValue, identified = false, nic = '') {
  const dateOfBirth = toIsoDate(value.birthYear, value.birthMonth, value.birthDay);

  const phone = phoneProblem(value.phone);
  const contactPhone = phoneProblem(value.contactPhone);
  const dateOfBirthError = dateOfBirthProblem(dateOfBirth);
  const nicBirthYear = nicBirthYearProblem(nic, value.birthYear);

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
    nicBirthYear !== null ||
    missing.phone ||
    missing.address ||
    missing.dateOfBirth;

  return {
    isoDate: dateOfBirth,
    phone,
    contactPhone,
    dateOfBirth: dateOfBirthError,
    nicBirthYear,
    dateIncomplete,
    missing,
    blocked,
  };
}

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

export function usePatientForm(initial: PatientFormValue) {
  const [value, setValue] = useState(initial);

  function set<K extends keyof PatientFormValue>(key: K, next: PatientFormValue[K]) {
    setValue((current) => ({ ...current, [key]: next }));
  }

  return { value, set, replace: setValue };
}

export function Counter({ value, limit }: { value: string; limit: number }) {
  if (value.length < limit * 0.75) {
    return null;
  }

  const left = limit - value.length;

  return (
    <p className={left === 0 ? 'field-error' : 'hint'}>
      {left === 0 ? `Limit reached - ${limit} characters.` : `${left} characters left.`}
    </p>
  );
}

export function PatientFields({
  value,
  set,
  idPrefix,
  identified = false,
  lockGenderTo,
  allowUnknownGender = false,
  nic = '',
}: {
  value: PatientFormValue;
  set: <K extends keyof PatientFormValue>(key: K, next: PatientFormValue[K]) => void;

  idPrefix: string;

  identified?: boolean;
  lockGenderTo?: Gender;
  /// For an unidentified arrival only — hard rule H3 (patient-management-plan.md §9) sends
  /// `unknown` to a mixed ward by rule rather than guessing a single-sex ward from appearance.
  allowUnknownGender?: boolean;
  /// The patient's NIC, if known — cross-checked against the birth year (Sri Lankan NICs
  /// encode it in the leading digits). Omit for an unidentified arrival with no NIC yet.
  nic?: string;
}) {
  const problems = patientFormProblems(value, identified, nic);

  const baseGenderOptions: Gender[] = allowUnknownGender
    ? [...selectableGenders, 'unknown']
    : selectableGenders;

  const genderOptions = baseGenderOptions.includes(value.gender)
    ? baseGenderOptions
    : [...baseGenderOptions, value.gender];

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
            disabled={lockGenderTo !== undefined}
            onChange={(event) => set('gender', event.target.value as Gender)}
          >
            {genderOptions.map((option) => (
              <option key={option} value={option}>
                {genderLabels[option]}
              </option>
            ))}
          </select>
          {lockGenderTo && (
            <p className="hint">Locked to {genderLabels[lockGenderTo]} for this care level.</p>
          )}
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
              {identified ? 'Select all three.' : 'Select all three, or leave all three blank.'}
            </p>
          )}
          {problems.missing.dateOfBirth && (
            <p className="field-error">
              Required. Ward eligibility depends on the patient's age.
            </p>
          )}
          {problems.nicBirthYear && <p className="field-error">{problems.nicBirthYear}</p>}
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

          <label htmlFor={`${idPrefix}-contact-name`}>
            Emergency/guardian contact name{' '}
            {identified && <span className="muted">(optional)</span>}
          </label>
          <input
            id={`${idPrefix}-contact-name`}
            value={value.contactName}
            maxLength={fieldLimits.contactName}
            placeholder="Nilanthi Gunawardena"
            onChange={(event) => set('contactName', event.target.value)}
          />
          <p className="hint">Who to call in an emergency.</p>
        </div>
        <div className="field">
          <label htmlFor={`${idPrefix}-contact-phone`}>
            Emergency/guardian contact number{' '}
            {identified && <span className="muted">(optional)</span>}
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

export function onSubmit(handler: () => void) {
  return (event: FormEvent) => {
    event.preventDefault();
    handler();
  };
}
