import { Table } from '../components/Table';
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
import { ActionDialog } from '../components/ui/action-dialog';
import {
  genderPolicies,
  genderPolicyLabels,
  wardTypeHints,
  wardTypeLabels,
  wardTypes,
} from '../types/wards';
import { AppSelect } from '../components/ui/app-select';

export function WardsPage() {
  const session = useSession();
  const queryClient = useQueryClient();

  const [wardType, setWardType] = useState<WardType | ''>('');
  const [isActive, setIsActive] = useState(true);
  const [createOpen, setCreateOpen] = useState(false);

  const wards = useQuery(
    listWardsOptions({ query: { ...(wardType ? { wardType } : {}), isActive } }),
  );

  const create = useMutation({
    ...createWardMutation(),
    onSuccess: (ward) => {
      toast.success(`Ward ${ward.name} created.`);

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
            <AppSelect
              id="filter-type"
              label="Ward type"
              value={wardType}
              onValueChange={(value) => setWardType(value as WardType | '')}
              options={[
                { value: '', label: 'All types' },
                ...wardTypes.map((type) => ({ value: type, label: wardTypeLabels[type] })),
              ]}
            />
          </div>
          <div>
            <AppSelect
              id="filter-active"
              label="Status"
              value={isActive ? 'active' : 'retired'}
              onValueChange={(value) => setIsActive(value === 'active')}
              options={[
                { value: 'active', label: 'Active' },
                { value: 'retired', label: 'Retired' },
              ]}
            />
          </div>
        </div>
      </div>

      {canCreateWard(session?.principal.role) && (
        <>
          <button type="button" onClick={() => setCreateOpen(true)}>Add ward</button>
          <ActionDialog title="Add ward" isOpen={createOpen} onClose={() => setCreateOpen(false)}>
            {createOpen && <CreateWardCard
              isPending={create.isPending}
              onCreate={(body, done) => create.mutate({ body }, { onSuccess: () => { done(); setCreateOpen(false); } })}
            />}
          </ActionDialog>
        </>
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
        Gender policy is a property of the ward and the bed agent cannot override it. ICU and
        pediatric wards are mixed, because intensive care units are open bays.
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
            <AppSelect
              id="new-type"
              label="Ward type"
              value={wardType}
              onValueChange={(value) => setWardType(value as WardType)}
              options={wardTypes.map((type) => ({ value: type, label: wardTypeLabels[type] }))}
            />
            <p className="hint">{wardTypeHints[wardType]}</p>
          </div>
          <div className="field">
            <AppSelect
              id="new-policy"
              label="Gender policy"
              value={genderPolicy}
              onValueChange={(value) => setGenderPolicy(value as GenderPolicy)}
              options={genderPolicies.map((policy) => ({ value: policy, label: genderPolicyLabels[policy] }))}
            />
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
    <Table>
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
    </Table>
  );
}
