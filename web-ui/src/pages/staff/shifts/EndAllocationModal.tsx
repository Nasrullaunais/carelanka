import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import { toast } from 'sonner';
import { endAllocationMutation } from '../../../services/api/generated/@tanstack/react-query.gen';
import type { AllocationDto, AllocationEndReason, EndAllocationRequest, ShiftDetailDto } from '../../../services/api/generated';
import { ModalDialog } from './ModalDialog';
import { ALLOCATION_END_REASONS } from './constants';

export function EndAllocationModal({
  allocation,
  shift,
  onClose,
  onSuccess,
}: {
  allocation: AllocationDto;
  shift: ShiftDetailDto;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [reason, setReason] = useState<AllocationEndReason>('manual');
  const [notes, setNotes] = useState('');
  const [suppressAgent, setSuppressAgent] = useState(false);

  const endMutation = useMutation({
    ...endAllocationMutation(),
    onSuccess: (res) => {
      toast.success(`Allocation for ${allocation.staff_name} ended.`);
      if (res.roster_proposal_id) {
        toast.info(
          `Ward coverage shortfall detected. Automatic AI Roster proposal triggered (ID: ${res.roster_proposal_id.slice(0, 8)}…).`,
          { duration: 6000 }
        );
      }
      onSuccess();
    },
    onError: (err) => {
      const msg =
        (err as { detail?: string; message?: string })?.detail ??
        (err as { message?: string })?.message ??
        'Failed to end allocation';
      toast.error(msg);
    },
  });

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const body: EndAllocationRequest = {
      reason,
      notes: notes.trim() || null,
      suppress_agent: suppressAgent,
    };
    endMutation.mutate({ path: { id: allocation.id }, body });
  }

  return (
    <ModalDialog title={`End allocation: ${allocation.staff_name}`} onClose={onClose} maxWidth="34rem">
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem' }}>
        <p style={{ margin: 0, fontSize: '0.9rem' }}>
          Are you sure you want to end <strong>{allocation.staff_name}</strong>'s allocation on{' '}
          <strong>{shift.ward_name}</strong> ({shift.date}, {shift.start_time} – {shift.end_time})?
        </p>

        <div>
          <label htmlFor="end-allocation-reason">End reason *</label>
          <select
            id="end-allocation-reason"
            value={reason}
            onChange={(e) => setReason(e.target.value as AllocationEndReason)}
            required
          >
            {ALLOCATION_END_REASONS.map((r) => (
              <option key={r.value} value={r.value}>{r.label}</option>
            ))}
          </select>
        </div>

        <div>
          <label htmlFor="end-allocation-notes">Notes (optional)</label>
          <textarea
            id="end-allocation-notes"
            rows={2}
            placeholder="Add relevant administrative notes or explanation…"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            maxLength={500}
          />
        </div>

        <div style={{ padding: '0.6rem 0.8rem', background: 'var(--surface-sunken, #f8fafc)', borderRadius: '0.375rem', border: '1px solid var(--line)' }}>
          <label style={{ display: 'flex', alignItems: 'flex-start', gap: '0.5rem', fontSize: '0.85rem', cursor: 'pointer', margin: 0 }}>
            <input
              type="checkbox"
              checked={suppressAgent}
              onChange={(e) => setSuppressAgent(e.target.checked)}
              style={{ marginTop: '0.2rem' }}
            />
            <div>
              <span>Suppress automatic AI Roster Agent proposal</span>
              <p className="muted" style={{ margin: '0.15rem 0 0', fontSize: '0.75rem' }}>
                If unchecked and ending this allocation drops ward headcount below minimum, the Staff Allocation
                Agent will automatically formulate a cascading swap proposal.
              </p>
            </div>
          </label>
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
          <button
            type="button"
            className="secondary"
            onClick={onClose}
            disabled={endMutation.isPending}
          >
            Cancel
          </button>
          <button
            type="submit"
            style={{ background: 'var(--danger, #b91c1c)', borderColor: 'var(--danger, #b91c1c)' }}
            disabled={endMutation.isPending}
          >
            {endMutation.isPending ? 'Ending allocation…' : 'End allocation'}
          </button>
        </div>
      </form>
    </ModalDialog>
  );
}
