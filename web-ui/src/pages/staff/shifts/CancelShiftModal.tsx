import { useMutation } from '@tanstack/react-query';
import { toast } from 'sonner';
import { cancelShiftMutation } from '../../../services/api/generated/@tanstack/react-query.gen';
import type { ShiftDetailDto, ShiftSummaryDto } from '../../../services/api/generated';
import { ModalDialog } from './ModalDialog';

export function CancelShiftModal({
  shift,
  onClose,
  onSuccess,
}: {
  shift: ShiftSummaryDto | ShiftDetailDto;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const cancelMutation = useMutation({
    ...cancelShiftMutation(),
    onSuccess: () => {
      toast.success('Shift cancelled successfully');
      onSuccess();
    },
    onError: (err) => {
      const msg =
        (err as { detail?: string; message?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Failed to cancel shift';
      toast.error(msg);
    },
  });

  function handleConfirm() {
    cancelMutation.mutate({ path: { id: shift.id } });
  }

  return (
    <ModalDialog title="Cancel scheduled shift" onClose={onClose} maxWidth="32rem">
      <div style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem' }}>
        <p>
          Are you sure you want to cancel the shift on <strong>{shift.date}</strong> for{' '}
          <strong>{shift.ward_name}</strong> ({shift.start_time} – {shift.end_time})?
        </p>

        <p className="muted" style={{ fontSize: '0.85rem' }}>
          Cancelling this shift removes the shift slot and automatically releases any staff allocations
          associated with it. This action cannot be undone.
        </p>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
          <button
            type="button"
            className="secondary"
            onClick={onClose}
            disabled={cancelMutation.isPending}
          >
            Keep shift
          </button>
          <button
            type="button"
            style={{ background: 'var(--danger, #b91c1c)', borderColor: 'var(--danger, #b91c1c)' }}
            onClick={handleConfirm}
            disabled={cancelMutation.isPending}
          >
            {cancelMutation.isPending ? 'Cancelling…' : 'Confirm cancellation'}
          </button>
        </div>
      </div>
    </ModalDialog>
  );
}
