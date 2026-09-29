import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import { toast } from 'sonner';
import { createShiftMutation } from '../../../services/api/generated/@tanstack/react-query.gen';
import type { CreateShiftRequest, StaffRole } from '../../../services/api/generated';
import { localDay } from '../../../types/datetime';
import { ModalDialog } from './ModalDialog';
import { ShiftFormFields, validateShiftFields } from './ShiftFormFields';

export function CreateShiftModal({
  wards,
  onClose,
  onSuccess,
}: {
  wards: Array<{ id: string; name: string }>;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [wardId, setWardId] = useState(wards[0]?.id ?? '');
  const [date, setDate] = useState(() => localDay(new Date()));
  const [startTime, setStartTime] = useState('08:00');
  const [endTime, setEndTime] = useState('16:00');
  const [requiredRole, setRequiredRole] = useState<StaffRole>('ward_nurse');
  const [requiredSkillId, setRequiredSkillId] = useState('');
  const [headcountNeeded, setHeadcountNeeded] = useState(2);
  const [minimumHeadcount, setMinimumHeadcount] = useState(1);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const createMutation = useMutation({
    ...createShiftMutation(),
    onSuccess: () => {
      toast.success('Shift created successfully');
      onSuccess();
    },
    onError: (err) => {
      const msg =
        (err as { detail?: string; message?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Failed to create shift';
      toast.error(msg);
    },
  });

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const errors = validateShiftFields({ wardId, date, startTime, endTime, headcountNeeded, minimumHeadcount });
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }
    setFieldErrors({});

    const body: CreateShiftRequest = {
      ward_id: wardId,
      date,
      start_time: startTime,
      end_time: endTime,
      required_role: requiredRole,
      required_skill_id: requiredSkillId || null,
      headcount_needed: Number(headcountNeeded),
      minimum_headcount: Number(minimumHeadcount),
    };

    createMutation.mutate({ body });
  }

  return (
    <ModalDialog title="Create single shift" onClose={onClose} maxWidth="36rem">
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem' }}>
        <ShiftFormFields
          idPrefix="create-shift"
          wardId={wardId}
          onWardChange={setWardId}
          wards={wards}
          date={date}
          onDateChange={setDate}
          startTime={startTime}
          onStartTimeChange={setStartTime}
          endTime={endTime}
          onEndTimeChange={setEndTime}
          requiredRole={requiredRole}
          onRequiredRoleChange={setRequiredRole}
          requiredSkillId={requiredSkillId}
          onRequiredSkillIdChange={setRequiredSkillId}
          headcountNeeded={headcountNeeded}
          onHeadcountNeededChange={setHeadcountNeeded}
          minimumHeadcount={minimumHeadcount}
          onMinimumHeadcountChange={setMinimumHeadcount}
          fieldErrors={fieldErrors}
        />

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
          <button type="button" className="secondary" onClick={onClose} disabled={createMutation.isPending}>
            Cancel
          </button>
          <button type="submit" disabled={createMutation.isPending}>
            {createMutation.isPending ? 'Creating…' : 'Create shift'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}
