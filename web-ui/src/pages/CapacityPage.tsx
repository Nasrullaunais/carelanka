import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  getWardCapacityOptions,
  getWardOccupancyOptions,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { WardCapacity } from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { canReadCapacity } from '../types/permissions';
import { admissionCategoryLabels } from '../types/patients';
import { genderPolicyLabels, wardTypeLabels } from '../types/wards';

// How full the hospital is, ward by ward. Counts only — no names, no records, nothing about
// any individual patient — which is why every staff role can open it.
//
// Two endpoints, and they answer different questions on purpose:
//
//   GET /capacity/wards        how many beds are free right now, everywhere
//   GET /wards/{id}/occupancy  where the people in ONE ward's beds are up to
//
// A free bed is one that is usable, empty, and not under a live hold. A hold that has run out
// counts as free again, and that rule lives in one service so nothing re-invents it.

export function CapacityPage() {
  const session = useSession();
  const [selected, setSelected] = useState<WardCapacity | null>(null);

  // Same reason as the appointments desk: the hook cannot go behind the early return, so it
  // is switched off instead. Nobody without a staff role should be asking the server at all.
  const isStaffMember = canReadCapacity(session?.principal.role);

  const capacity = useQuery({ ...getWardCapacityOptions(), enabled: isStaffMember });

  if (!isStaffMember) {
    return (
      <>
        <h1>Bed capacity</h1>
        <p className="empty">Sign in as a staff member to see bed numbers.</p>
      </>
    );
  }

  const wards = capacity.data?.wards ?? [];
  const totalBeds = wards.reduce((sum, ward) => sum + ward.total_beds, 0);
  const freeBeds = wards.reduce((sum, ward) => sum + ward.free_beds, 0);

  return (
    <>
      <h1>Bed capacity</h1>
      <p className="muted">
        Every ward, how many beds it has, and how many of them you could put someone in this
        minute. Numbers only — nothing here says who is in a bed.
      </p>

      {capacity.isError ? (
        <div className="card">
          <div className="empty">
            <p>Could not count the beds.</p>
            <button type="button" className="secondary" onClick={() => void capacity.refetch()}>
              Try again
            </button>
          </div>
        </div>
      ) : (
        <>
          <div className="card">
            <h2>The whole hospital</h2>

            <div className="stats">
              <Stat caption="Beds in total" value={totalBeds} />
              <Stat caption="Free right now" value={freeBeds} tone={freeBeds === 0 ? 'none' : 'free'} />
              <Stat caption="Not free" value={totalBeds - freeBeds} />
            </div>

            <p className="hint">
              &ldquo;Not free&rdquo; is everything else at once: someone in the bed, someone
              holding it on their way in, or Equipment has it out for repair. Open a ward below
              to see which is which.
              {capacity.data && (
                <>
                  {' '}
                  Counted at{' '}
                  <strong>
                    {new Date(capacity.data.generated_at).toLocaleTimeString(undefined, {
                      hour: '2-digit',
                      minute: '2-digit',
                      second: '2-digit',
                    })}
                  </strong>
                  . Free beds go stale in seconds, so the count carries its own timestamp.
                </>
              )}
            </p>

            <button
              type="button"
              className="secondary"
              disabled={capacity.isFetching}
              onClick={() => void capacity.refetch()}
            >
              {capacity.isFetching ? 'Counting…' : 'Count again'}
            </button>
          </div>

          <div className="card">
            <h2>Ward by ward</h2>

            {capacity.isLoading ? (
              <p className="empty">Loading…</p>
            ) : wards.length === 0 ? (
              <p className="empty">There are no wards yet.</p>
            ) : (
              <table>
                <thead>
                  <tr>
                    <th>Ward</th>
                    <th>Type</th>
                    <th>Who it takes</th>
                    <th>Beds</th>
                    <th>Free</th>
                    <th>How full</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {wards.map((ward) => (
                    <tr key={ward.ward_id}>
                      <td>
                        <strong>{ward.name}</strong>
                      </td>
                      <td>{wardTypeLabels[ward.ward_type]}</td>
                      <td>
                        {/* On the table because a male-only ward with two free beds is no use
                            to a female patient, and the reader has to be able to see that. */}
                        {genderPolicyLabels[ward.gender_policy]}
                      </td>
                      <td>{ward.total_beds}</td>
                      <td>
                        <span className={ward.free_beds === 0 ? 'badge retired' : 'badge'}>
                          {ward.free_beds === 0 ? 'Full' : ward.free_beds}
                        </span>
                      </td>
                      <td>
                        <Meter total={ward.total_beds} free={ward.free_beds} />
                      </td>
                      <td>
                        <button
                          type="button"
                          className="secondary"
                          onClick={() =>
                            setSelected((current) =>
                              current?.ward_id === ward.ward_id ? null : ward,
                            )
                          }
                        >
                          {selected?.ward_id === ward.ward_id ? 'Close' : 'Open'}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}

            <p className="hint">
              A ward with no beds at all is not a mistake here — beds are registered by
              Equipment, and a ward exists before anyone puts furniture in it.
            </p>
          </div>

          {selected && <WardOccupancyCard ward={selected} onClose={() => setSelected(null)} />}
        </>
      )}
    </>
  );
}

// ---------------------------------------------------------------------------
// One ward, broken down
// ---------------------------------------------------------------------------

function WardOccupancyCard({ ward, onClose }: { ward: WardCapacity; onClose: () => void }) {
  const occupancy = useQuery(getWardOccupancyOptions({ path: { id: ward.ward_id } }));

  return (
    <div className="card">
      <h2>{ward.name}</h2>

      {occupancy.isLoading && <p className="empty">Loading…</p>}

      {occupancy.isError && (
        <div className="empty">
          <p>Could not load this ward.</p>
          <button type="button" className="secondary" onClick={() => void occupancy.refetch()}>
            Try again
          </button>
        </div>
      )}

      {occupancy.data && (
        <>
          <div className="stats">
            <Stat caption="Beds in total" value={occupancy.data.total_beds} />
            <Stat caption="Patient in it" value={occupancy.data.occupied_beds} />
            <Stat caption="Being held" value={occupancy.data.reserved_beds} />
            <Stat caption="Out for repair" value={occupancy.data.out_of_service_beds} />
            {/* Free comes from the capacity count, not from subtracting the four numbers above.
                One service owns what "free" means; working it out a second way here is how two
                screens end up disagreeing by one bed. */}
            <Stat
              caption="Free right now"
              value={ward.free_beds}
              tone={ward.free_beds === 0 ? 'none' : 'free'}
            />
          </div>

          <p className="hint">
            &ldquo;Being held&rdquo; is a bed kept for someone on their way in. A hold lasts 30
            minutes; once it runs out the bed is free again on its own, without anyone
            releasing it.
          </p>

          <h2 style={{ marginTop: '1.25rem' }}>Who is in the beds</h2>
          <p className="muted" style={{ marginBottom: '0.9rem' }}>
            Fifteen ordinary inpatients and two high-dependency patients are both
            &ldquo;seventeen patients&rdquo;, and they need very different numbers of staff on
            the floor. This is that difference. It counts only people actually in a bed —
            someone still on their way is in the next line down.
          </p>

          <table>
            <tbody>
              {Object.entries(occupancy.data.patients_by_category).map(([category, count]) => (
                <tr key={category}>
                  <th scope="row">
                    {admissionCategoryLabels[
                      category as keyof typeof admissionCategoryLabels
                    ] ?? category.replaceAll('_', ' ')}
                  </th>
                  <td>{count}</td>
                </tr>
              ))}
              <tr>
                <th scope="row">On their way, next 2 hours</th>
                <td>
                  <strong>{occupancy.data.incoming_next_2h}</strong>
                </td>
              </tr>
            </tbody>
          </table>

          <p className="hint">
            Every care level is listed even at zero, so a line does not vanish from a chart the
            moment it empties.
          </p>
        </>
      )}

      <button
        type="button"
        className="secondary"
        style={{ marginTop: '0.9rem' }}
        onClick={onClose}
      >
        Close
      </button>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Small pieces
// ---------------------------------------------------------------------------

function Stat({
  caption,
  value,
  tone,
}: {
  caption: string;
  value: number;
  tone?: 'free' | 'none';
}) {
  return (
    <div className={tone ? `stat ${tone}` : 'stat'}>
      <div className="value">{value}</div>
      <div className="caption">{caption}</div>
    </div>
  );
}

/** A bar the length of the ward, shaded for the part of it that is not free. */
function Meter({ total, free }: { total: number; free: number }) {
  if (total === 0) {
    return <span className="muted">No beds</span>;
  }

  const usedShare = Math.round(((total - free) / total) * 100);

  return (
    <span
      className={free === 0 ? 'meter full' : 'meter'}
      role="img"
      aria-label={`${free} of ${total} beds free`}
      title={`${free} of ${total} beds free`}
    >
      <span style={{ width: `${usedShare}%` }} />
    </span>
  );
}
