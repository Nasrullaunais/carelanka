import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import { toast } from 'sonner';
import { updateShiftMutation } from '../../../services/api/generated/@tanstack/react-query.gen';
import type { CreateShiftRequest, ShiftDetailDto, ShiftSummaryDto, StaffRole } from '../../../services/api/generated';
import { ModalDialog } from './ModalDialog';
import { ShiftFormFields, validateShiftFields } from './ShiftFormFields';

export function EditShiftModal({
  shift,
  wards,
  onClose,
  onSuccess,
}: {
  shift: ShiftDetailDto | ShiftSummaryDto;
  wards: Array<{ id: string; name: string }>;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [wardId, setWardId] = useState(shift.ward_id);
  const [date, setDate] = useState(shift.date);
  const [startTime, setStartTime] = useState(shift.start_time);
  const [endTime, setEndTime] = useState(shift.end_time);
  const [requiredRole, setRequiredRole] = useState<StaffRole>(shift.required_role);
  const [requiredSkillId, setRequiredSkillId] = useState(shift.required_skill_id ?? '');
  const [headcountNeeded, setHeadcountNeeded] = useState(shift.headcount_needed);
  const [minimumHeadcount, setMinimumHeadcount] = useState(shift.minimum_headcount);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const updateMutation = useMutation({
    ...updateShiftMutation(),
    onSuccess: () => {
      toast.success('Shift updated successfully');
      onSuccess();
    },
    onError: (err) => {
      const msg =
        (err as { detail?: string; message?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Failed to update shift';
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

    updateMutation.mutate({ path: { id: shift.id }, body });
  }

  return (
    <ModalDialog title={`Edit shift: ${shift.ward_name} · ${shift.date}`} onClose={onClose} maxWidth="36rem">
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem' }}>
        <ShiftFormFields
          idPrefix="edit-shift"
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
          <button type="button" className="secondary" onClick={onClose} disabled={updateMutation.isPending}>
            Cancel
          </button>
          <button type="submit" disabled={updateMutation.isPending}>
            {updateMutation.isPending ? 'Saving changes…' : 'Save changes'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}
