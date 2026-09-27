import { useEffect, useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, CardContent, CardTitle } from '@heroui/react';
import { toast } from 'sonner';
import {
  assignCurrentAmbulanceCrewMutation,
  getCurrentAmbulanceCrewOptions,
  searchAvailableCrewOptions,
  unassignCurrentAmbulanceCrewMutation,
} from '../../../services/api/generated/@tanstack/react-query.gen';
import { QueryState } from '../../../components/ui/query-state';
import { SearchMultiSelect, type SearchOption } from '../../../components/ui/search-multi-select';
import { isConflict, problemMessage, type ApiProblem } from '../../../services/api/errors';
import { invalidateEmergencyQueries } from '../query-invalidation';

export function CrewManagement({ ambulanceId, registrationNumber }: {
  ambulanceId: string;
  registrationNumber: string;
}) {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [selected, setSelected] = useState<SearchOption[]>([]);
  const [assignError, setAssignError] = useState('');
  const [assigning, setAssigning] = useState(false);

  useEffect(() => {
    const timeout = window.setTimeout(() => setDebouncedSearch(search), 250);
    return () => window.clearTimeout(timeout);
  }, [search]);

  const crew = useQuery(getCurrentAmbulanceCrewOptions({ path: { id: ambulanceId } }));
  const candidates = useQuery({
    ...searchAvailableCrewOptions({ query: { search: debouncedSearch } }),
    staleTime: 5_000,
  });
  const refresh = () => invalidateEmergencyQueries(queryClient);
  const assign = useMutation(assignCurrentAmbulanceCrewMutation());
  const unassign = useMutation({
    ...unassignCurrentAmbulanceCrewMutation(),
    onSuccess: () => {
      toast.success('Crew member unassigned.');
      void refresh();
      void candidates.refetch();
    },
    onError: (error) => {
      if (isConflict(error)) {
        toast.error('Crew is locked while this ambulance has a live dispatch.');
        void refresh();
      }
    },
  });

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (assigning || selected.length === 0) return;
    setAssigning(true);
    setAssignError('');
    const failures: SearchOption[] = [];
    let assignedCount = 0;
    for (const person of selected) {
      try {
        await assign.mutateAsync({ path: { id: ambulanceId }, body: { staff_member_id: person.id } });
        assignedCount++;
      } catch (error) {
        failures.push(person);
        if (isConflict(error as ApiProblem)) {
          setAssignError('One or more crew members could not be assigned. The ambulance may have a live dispatch, or a person may have been assigned elsewhere.');
        } else {
          setAssignError(problemMessage(error as ApiProblem, 'One or more crew members could not be assigned.') ?? 'One or more crew members could not be assigned.');
        }
      }
    }
    setSelected(failures);
    if (assignedCount > 0) toast.success(`${assignedCount} crew member${assignedCount === 1 ? '' : 's'} assigned.`);
    await refresh();
    await candidates.refetch();
    setAssigning(false);
  }

  const options: SearchOption[] = (candidates.data ?? []).map((candidate) => ({
    id: candidate.staff_member_id,
    label: candidate.full_name,
  }));

  return (
    <Card>
      <CardContent className="flex flex-col gap-4">
        <div>
          <CardTitle>Crew · {registrationNumber}</CardTitle>
          <p className="mt-1 text-sm text-muted">Search available ambulance crew by name. Select one or more people, then assign them with one action.</p>
        </div>
        <QueryState query={crew} errorContext="Could not load current crew." isEmpty={(members) => members.length === 0} emptyMessage="No crew assigned yet.">
          {(members) => (
            <div>
              <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">Current crew</p>
              <ul className="flex flex-col gap-2">
                {members.map((member) => (
                  <li key={member.staff_member_id} className="flex items-center justify-between gap-3 rounded-lg border border-default-200 px-3 py-2">
                    <span>{member.full_name ?? member.staff_member_id}</span>
                    <Button
                      size="sm"
                      variant="outline"
                      isDisabled={unassign.isPending || assigning}
                      onPress={() => member.staff_member_id && unassign.mutate({ path: { ambulanceId, staffMemberId: member.staff_member_id } })}
                    >
                      Unassign
                    </Button>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </QueryState>
        <form className="flex flex-col gap-3" onSubmit={(event) => void submit(event)}>
          <SearchMultiSelect
            label="Find crew members"
            placeholder="Search by name"
            search={search}
            onSearchChange={setSearch}
            options={options}
            selected={selected}
            onChange={setSelected}
            isLoading={search !== debouncedSearch || candidates.isPending || candidates.isFetching}
            error={candidates.isError ? 'Could not search crew. Try again in a moment.' : undefined}
            onRetry={() => void candidates.refetch()}
            isDisabled={assigning}
          />
          {assignError && <p role="alert" className="m-0 text-sm text-danger">{assignError} Failed selections remain selected for retry.</p>}
          <div><Button type="submit" isDisabled={assigning || selected.length === 0}>{assigning ? 'Assigning…' : `Assign ${selected.length} crew member${selected.length === 1 ? '' : 's'}`}</Button></div>
        </form>
      </CardContent>
    </Card>
  );
}
