import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, CardContent, CardTitle } from '@heroui/react';
import { toast } from 'sonner';
import {
  assignCurrentAmbulanceCrewMutation,
  getCurrentAmbulanceCrewOptions,
  unassignCurrentAmbulanceCrewMutation,
} from '../../../services/api/generated/@tanstack/react-query.gen';
import { QueryState } from '../../../components/ui/query-state';
import { isConflict } from '../../../services/api/errors';
import { invalidateEmergencyQueries } from '../query-invalidation';
import { StaffPickerField, type StaffPickerOption } from './staff-picker-field';

export function CrewManagement({ ambulanceId, registrationNumber, lookup }: {
  ambulanceId: string;
  registrationNumber: string;
  lookup?: (query: string) => StaffPickerOption[];
}) {
  const queryClient = useQueryClient();
  const [staffId, setStaffId] = useState('');
  const crew = useQuery(getCurrentAmbulanceCrewOptions({ path: { id: ambulanceId } }));
  const refresh = () => invalidateEmergencyQueries(queryClient);
  const assign = useMutation({
    ...assignCurrentAmbulanceCrewMutation(),
    onSuccess: () => {
      setStaffId('');
      toast.success('Crew member assigned.');
      void refresh();
    },
    onError: (error) => {
      if (isConflict(error)) {
        toast.error('Crew is locked while this ambulance has a live dispatch.');
        void refresh();
      }
    },
  });
  const unassign = useMutation({
    ...unassignCurrentAmbulanceCrewMutation(),
    onSuccess: () => {
      toast.success('Crew member unassigned.');
      void refresh();
    },
    onError: (error) => {
      if (isConflict(error)) {
        toast.error('Crew is locked while this ambulance has a live dispatch.');
        void refresh();
      }
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    const id = staffId.trim();
    if (id) assign.mutate({ path: { id: ambulanceId }, body: { staff_member_id: id } });
  }

  return (
    <Card>
      <CardContent className="flex flex-col gap-3">
        <CardTitle>Crew · {registrationNumber}</CardTitle>
        <QueryState query={crew} errorContext="Could not load current crew." isEmpty={(members) => members.length === 0} emptyMessage="No crew assigned yet.">
          {(members) => (
            <ul className="flex flex-col gap-2">
              {members.map((member) => (
                <li key={member.staff_member_id} className="flex items-center justify-between gap-3">
                  <span>{member.full_name ?? member.staff_member_id}</span>
                  <Button
                    size="sm"
                    variant="outline"
                    isDisabled={unassign.isPending}
                    onPress={() => member.staff_member_id && unassign.mutate({ path: { ambulanceId, staffMemberId: member.staff_member_id } })}
                  >
                    Unassign
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </QueryState>
        <form className="flex flex-wrap items-end gap-2" onSubmit={submit}>
          <StaffPickerField value={staffId} onChange={setStaffId} lookup={lookup} />
          <Button type="submit" isDisabled={assign.isPending || staffId.trim() === ''}>{assign.isPending ? 'Assigning…' : 'Assign crew member'}</Button>
        </form>
      </CardContent>
    </Card>
  );
}
