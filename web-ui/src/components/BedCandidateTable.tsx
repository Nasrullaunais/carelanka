import type { Ward } from '../services/api/generated';
import type { Placement } from '../types/beds';
import { genderPolicyLabels, wardTypeLabels } from '../types/wards';

export interface BedCandidateBed {
  id: string;
  ward_id: string;
  ward_name: string;
  bed_number: string;
  has_isolation: boolean;
}

export interface BedCandidate {
  bed: BedCandidateBed;
  ward: Ward | undefined;
  placement: Placement;
}

/**
 * The full free-bed table used by the "Assign a bed" and "Change bed" drawers.
 */
export function BedCandidateTable({
  candidates,
  writing,
  missing,
  actionLabel,
  onPick,
}: {
  candidates: BedCandidate[];
  writing: boolean;
  missing: number;
  actionLabel: (placement: Placement) => string;
  onPick: (bed: BedCandidateBed, placement: Placement) => void;
}) {
  const usable = candidates.filter((candidate) => candidate.placement.kind !== 'refused');
  const overrides = candidates.filter((candidate) => candidate.placement.kind === 'override');

  if (candidates.length === 0) {
    return (
      <p className="empty">
        No beds are free. The patient stays on the waiting list until one is — capacity is the
        duty manager's to resolve.
      </p>
    );
  }

  return (
    <>
      <table>
        <thead>
          <tr>
            <th>Bed</th>
            <th>Ward</th>
            <th>Accepts</th>
            <th>Isolation</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {candidates.map(({ bed, ward, placement }) => (
            <tr key={bed.id}>
              <td>
                <strong>{bed.bed_number}</strong>
              </td>
              <td>
                {bed.ward_name}
                <br />
                <span className="muted">{ward ? wardTypeLabels[ward.ward_type] : 'Unknown ward'}</span>
              </td>
              <td>{ward ? genderPolicyLabels[ward.gender_policy] : '—'}</td>
              <td>{bed.has_isolation ? 'Yes' : 'No'}</td>
              <td>
                {placement.kind === 'refused' ? (
                  <span className="muted">{placement.why}</span>
                ) : (
                  <>
                    <button
                      type="button"
                      className={placement.kind === 'override' ? 'warn' : undefined}
                      disabled={writing}
                      onClick={() => onPick(bed, placement)}
                    >
                      {actionLabel(placement)}
                    </button>
                    {placement.kind === 'override' && (
                      <p className="hint" style={{ marginTop: '0.25rem' }}>
                        {placement.why}
                      </p>
                    )}
                  </>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {usable.length === 0 && (
        <p className="empty">
          Beds are free, but none of them accepts this patient. The reason is on each row.
        </p>
      )}

      {missing > 0 && (
        <p className="field-error" style={{ marginTop: '0.6rem' }}>
          {missing} more free {missing === 1 ? 'bed is' : 'beds are'} not shown. This list is
          incomplete — report it before assigning a bed from it.
        </p>
      )}

      {overrides.length > 0 && (
        <p className="hint" style={{ marginTop: '0.6rem' }}>
          Amber buttons are beds outside the patient&rsquo;s care level. You may use one as duty
          manager; it is recorded as your decision, so give a reason in the note.
        </p>
      )}
    </>
  );
}
