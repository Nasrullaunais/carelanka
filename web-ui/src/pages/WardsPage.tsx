import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  createWardMutation,
  listWardsOptions,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { GenderPolicy, Ward, WardType } from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { canCreateWard } from '../types/permissions';
import {
  genderPolicies,
  genderPolicyLabels,
  wardTypeHints,
  wardTypeLabels,
  wardTypes,
} from '../types/wards';

export function WardsPage() {
  const session = useSession();
  const queryClient = useQueryClient();

  const [wardType, setWardType] = useState<WardType | ''>('');
  const [isActive, setIsActive] = useState(true);

  const wards = useQuery(
    listWardsOptions({ query: { ...(wardType ? { wardType } : {}), isActive } }),
  );

  const create = useMutation({
    ...createWardMutation(),
    onSuccess: (ward) => {
      toast.success(`Ward ${ward.name} created.`);

      // Invalidate every listWards query, not just the one currently on screen — the new
      // ward may belong to a filter the user has not selected yet. The generated key is a
      // single object, so this matches on its _id rather than on an array prefix.
      queryClient.invalidateQueries({
        predicate: (query) =>
          (query.queryKey[0] as { _id?: string } | undefined)?._id === 'listWards',
      });
    },
  });

  return (
    <>
      <h1>Wards</h1>
      <p className="muted">
        The ward register. Owned by Patient Management and read by all four components.
      </p>

      <div className="card">
        <h2>Filter</h2>
        <div className="row">
          <div>
            <label htmlFor="filter-type">Ward type</label>
            <select
              id="filter-type"
              value={wardType}
              onChange={(event) => setWardType(event.target.value as WardType | '')}
            >
              <option value="">All types</option>
              {wardTypes.map((type) => (
                <option key={type} value={type}>
                  {wardTypeLabels[type]}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="filter-active">Status</label>
            <select
              id="filter-active"
              value={isActive ? 'active' : 'retired'}
              onChange={(event) => setIsActive(event.target.value === 'active')}
            >
              <option value="active">Active</option>
              <option value="retired">Retired</option>
            </select>
          </div>
        </div>
      </div>

      {/* Hidden, not disabled: a control the user's role cannot use should not be on screen. */}
      {canCreateWard(session?.principal.role) && (
        <CreateWardCard
          isPending={create.isPending}
          onCreate={(body, done) => create.mutate({ body }, { onSuccess: done })}
        />
      )}

      <div className="card">
        <h2>{isActive ? 'Active wards' : 'Retired wards'}</h2>
        <WardTable
          isLoading={wards.isLoading}
          isError={wards.isError}
          onRetry={() => void wards.refetch()}
          wards={wards.data ?? []}
        />
      </div>
    </>
  );
}

type NewWard = {
  name: string;
  ward_type: WardType;
  gender_policy: GenderPolicy;
  is_active: boolean;
};

function CreateWardCard({
  isPending,
  onCreate,
}: {
  isPending: boolean;
  onCreate: (body: NewWard, done: () => void) => void;
}) {
  const [name, setName] = useState('');
  const [wardType, setWardType] = useState<WardType>('general');
  const [genderPolicy, setGenderPolicy] = useState<GenderPolicy>('mixed');

  function submit(event: FormEvent) {
    event.preventDefault();
    onCreate(
      {
        name: name.trim(),
        ward_type: wardType,
        gender_policy: genderPolicy,
        is_active: true,
      },
      () => setName(''),
    );
  }

  return (
    <div className="card">
      <h2>Create a ward</h2>
      <p className="muted" style={{ marginBottom: '0.9rem' }}>
        Gender policy is a property of the ward, not a rule the bed agent bends under
        pressure. ICU and pediatric wards are mixed because real intensive care units are
        open bays.
      </p>

      <form onSubmit={submit}>
        <div className="row">
          <div className="field">
            <label htmlFor="new-name">Name</label>
            <input
              id="new-name"
              value={name}
              maxLength={100}
              onChange={(event) => setName(event.target.value)}
              placeholder="ICU-1"
              required
            />
          </div>
          <div className="field">
            <label htmlFor="new-type">Ward type</label>
            <select
              id="new-type"
              value={wardType}
              onChange={(event) => setWardType(event.target.value as WardType)}
            >
              {wardTypes.map((type) => (
                <option key={type} value={type}>
                  {wardTypeLabels[type]}
                </option>
              ))}
            </select>
            <p className="hint">{wardTypeHints[wardType]}</p>
          </div>
          <div className="field">
            <label htmlFor="new-policy">Gender policy</label>
            <select
              id="new-policy"
              value={genderPolicy}
              onChange={(event) => setGenderPolicy(event.target.value as GenderPolicy)}
            >
              {genderPolicies.map((policy) => (
                <option key={policy} value={policy}>
                  {genderPolicyLabels[policy]}
                </option>
              ))}
            </select>
          </div>
        </div>

        <button type="submit" disabled={isPending || name.trim().length === 0}>
          {isPending ? 'Creating…' : 'Create ward'}
        </button>
      </form>
    </div>
  );
}

function WardTable({
  wards,
  isLoading,
  isError,
  onRetry,
}: {
  wards: Ward[];
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  if (isLoading) {
    return <p className="empty">Loading…</p>;
  }

  // The toast already fired. A list shows "Try again" rather than an empty table that looks
  // like a hospital with no wards.
  if (isError) {
    return (
      <div className="empty">
        <p>Could not load the ward list.</p>
        <button type="button" className="secondary" onClick={onRetry}>
          Try again
        </button>
      </div>
    );
  }

  if (wards.length === 0) {
    return <p className="empty">No wards match this filter.</p>;
  }

  return (
    <table>
      <thead>
        <tr>
          <th>Name</th>
          <th>Type</th>
          <th>Gender policy</th>
          <th>Beds</th>
          <th>Status</th>
        </tr>
      </thead>
      <tbody>
        {wards.map((ward) => (
          <tr key={ward.id}>
            <td>
              <strong>{ward.name}</strong>
            </td>
            <td>{wardTypeLabels[ward.ward_type]}</td>
            <td>{genderPolicyLabels[ward.gender_policy]}</td>
            <td>{ward.total_beds}</td>
            <td>
              <span className={ward.is_active ? 'badge' : 'badge retired'}>
                {ward.is_active ? 'Active' : 'Retired'}
              </span>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
