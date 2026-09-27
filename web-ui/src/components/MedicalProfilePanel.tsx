import { Table } from './Table';
import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  getPatientMedicalProfileOptions,
  getPatientMedicalProfileQueryKey,
  replacePatientMedicalProfileMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { PrincipalRole } from '../services/api/generated';
import { canWriteMedicalProfile } from '../types/permissions';
import { localDateTime } from '../types/datetime';

const KNOWN_CONDITIONS_MAX = 2000;
const ALLERGIES_MAX = 1000;
const CURRENT_SYMPTOMS_MAX = 2000;

type Draft = {
  known_conditions: string;
  allergies: string;
  current_symptoms: string;
};

const empty: Draft = {
  known_conditions: '',
  allergies: '',
  current_symptoms: '',
};

/**
 * The three boxes the care advisory agent reads. Everything in them is typed by a clinician -
 * there is no field here the system fills in, which is what keeps this a handover note rather
 * than a medical record.
 */
export function MedicalProfilePanel({
  patientId,
  patientName,
  role,
}: {
  patientId: string;
  patientName: string;
  role: PrincipalRole | undefined;
}) {
  const queryClient = useQueryClient();
  const writable = canWriteMedicalProfile(role);

  const profile = useQuery(getPatientMedicalProfileOptions({ path: { id: patientId } }));

  const [draft, setDraft] = useState<Draft>(empty);
  const [editing, setEditing] = useState(false);

  // Drop out of editing if the drawer is reused for a different patient, so a half-typed note
  // can never be saved onto somebody else's record.
  useEffect(() => setEditing(false), [patientId]);

  // The draft is seeded here rather than in an effect on `profile.data`: a background refetch
  // while somebody is typing would otherwise wipe what they had written.
  function startEditing() {
    setDraft({
      known_conditions: profile.data?.known_conditions ?? '',
      allergies: profile.data?.allergies ?? '',
      current_symptoms: profile.data?.current_symptoms ?? '',
    });

    setEditing(true);
  }

  const save = useMutation({
    ...replacePatientMedicalProfileMutation(),
    onSuccess: () => {
      toast.success(`Medical profile saved for ${patientName}.`);
      setEditing(false);

      queryClient.invalidateQueries({
        queryKey: getPatientMedicalProfileQueryKey({ path: { id: patientId } }),
      });
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();

    save.mutate({
      path: { id: patientId },
      body: {
        known_conditions: draft.known_conditions.trim() || null,
        allergies: draft.allergies.trim() || null,
        current_symptoms: draft.current_symptoms.trim() || null,
      },
    });
  }

  if (profile.isLoading) {
    return <p className="empty">Loading…</p>;
  }

  if (profile.isError) {
    return <p className="empty">Could not load the medical profile. Try again.</p>;
  }

  const written = profile.data?.updated_at;

  if (!editing) {
    const blank =
      !profile.data?.known_conditions &&
      !profile.data?.allergies &&
      !profile.data?.current_symptoms;

    return (
      <>
        {blank ? (
          <p className="empty">
            Nobody has recorded anything about this patient's health yet.
          </p>
        ) : (
          <Table>
            <tbody>
              <ProfileField label="Known conditions">
                {profile.data?.known_conditions}
              </ProfileField>
              <ProfileField label="Allergies">{profile.data?.allergies}</ProfileField>
              <ProfileField label="Current symptoms">
                {profile.data?.current_symptoms}
              </ProfileField>
            </tbody>
          </Table>
        )}

        {written && (
          <p className="muted" style={{ marginTop: '0.5rem', fontSize: '0.85rem' }}>
            Last written by {profile.data?.updated_by_staff_name ?? 'someone no longer on staff'}{' '}
            on {localDateTime(written)}.
          </p>
        )}

        {writable && (
          <button
            type="button"
            className="secondary"
            style={{ marginTop: '0.6rem' }}
            onClick={startEditing}
          >
            {blank ? 'Record medical details' : 'Edit'}
          </button>
        )}
      </>
    );
  }

  return (
    <form onSubmit={submit}>
      <p className="muted" style={{ fontSize: '0.85rem' }}>
        Saving replaces the whole profile — anything you clear here is cleared on the record.
      </p>

      <ProfileInput
        label="Known conditions"
        hint="Long-lived things: diabetes, asthma, hypertension."
        maxLength={KNOWN_CONDITIONS_MAX}
        value={draft.known_conditions}
        onChange={(value) => setDraft({ ...draft, known_conditions: value })}
      />

      <ProfileInput
        label="Allergies"
        hint="What they must not be given."
        maxLength={ALLERGIES_MAX}
        value={draft.allergies}
        onChange={(value) => setDraft({ ...draft, allergies: value })}
      />

      <ProfileInput
        label="Current symptoms"
        hint="What they are in with this time, and what led up to it: a fall last week, a course of antibiotics finished."
        maxLength={CURRENT_SYMPTOMS_MAX}
        value={draft.current_symptoms}
        onChange={(value) => setDraft({ ...draft, current_symptoms: value })}
      />

      <div style={{ display: 'flex', gap: '0.5rem', marginTop: '0.6rem' }}>
        <button type="submit" disabled={save.isPending}>
          {save.isPending ? 'Saving…' : 'Save'}
        </button>
        <button type="button" className="secondary" onClick={() => setEditing(false)}>
          Cancel
        </button>
      </div>
    </form>
  );
}

function ProfileField({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <tr>
      <th scope="row">{label}</th>
      <td>{children ?? <span className="muted">Not recorded</span>}</td>
    </tr>
  );
}

function ProfileInput({
  label,
  hint,
  maxLength,
  value,
  onChange,
}: {
  label: string;
  hint: string;
  maxLength: number;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <label style={{ display: 'block', marginTop: '0.6rem' }}>
      <span>{label}</span>
      <span className="muted" style={{ display: 'block', fontSize: '0.8rem' }}>
        {hint}
      </span>
      <textarea
        rows={3}
        maxLength={maxLength}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}
