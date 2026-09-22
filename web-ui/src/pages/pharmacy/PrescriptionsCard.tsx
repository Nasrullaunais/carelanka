import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  listPrescriptionsOptions,
  markPrescriptionDeliveredMutation,
  markPrescriptionReadyMutation,
  rejectPrescriptionMutation,
} from '../../services/api/generated/@tanstack/react-query.gen';
import { downloadPrescription } from '../../services/api/generated';
import type { Prescription, PrescriptionStatus } from '../../services/api/generated';
import { Dialog } from '../EquipmentPage';
import { prescriptionStatusLabels, prescriptionStatuses } from '../../types/pharmacy';

export function PrescriptionsCard() {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<PrescriptionStatus>('submitted');
  const [rejecting, setRejecting] = useState<Prescription | null>(null);

  const prescriptions = useQuery(listPrescriptionsOptions({ query: { status } }));
  const rows = prescriptions.data ?? [];

  function refresh() {
    queryClient.invalidateQueries({
      predicate: (query) =>
        (query.queryKey[0] as { _id?: string } | undefined)?._id === 'listPrescriptions',
    });
  }

  const ready = useMutation({
    ...markPrescriptionReadyMutation(),
    onSuccess: (prescription) => {
      toast.success(
        `Token ${prescription.token_number} issued to ${prescription.patient_name}. They can see it in the app.`,
      );
      refresh();
    },
  });

  const deliver = useMutation({
    ...markPrescriptionDeliveredMutation(),
    onSuccess: (prescription) => {
      toast.success(`Token ${prescription.token_number} delivered to ${prescription.patient_name}.`);
      refresh();
    },
  });

  const busy = ready.isPending || deliver.isPending;

  return (
    <div className="card">
      <h2>Prescriptions from the app</h2>
      <p className="muted" style={{ marginBottom: '0.9rem' }}>
        Patients send a photo of their prescription from the mobile app. Get the medicine
        ready, then mark it ready to give them a collection token for today. Mark it delivered
        when they collect it.
      </p>

      <div className="tabs">
        {prescriptionStatuses.map((value) => (
          <button
            key={value}
            type="button"
            aria-pressed={status === value}
            onClick={() => setStatus(value)}
          >
            {prescriptionStatusLabels[value]}
          </button>
        ))}
      </div>

      {prescriptions.isPending && <p className="empty">Loading prescriptions…</p>}

      {prescriptions.isError && (
        <p className="empty">
          Prescriptions could not be loaded.{' '}
          <button type="button" className="secondary" onClick={() => prescriptions.refetch()}>
            Try again
          </button>
        </p>
      )}

      {prescriptions.isSuccess && rows.length === 0 && (
        <p className="empty">Nothing here.</p>
      )}

      {rows.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>Received</th>
              <th>Patient</th>
              <th>Note</th>
              <th>Prescription</th>
              {status !== 'submitted' && <th>{status === 'rejected' ? 'Reason' : 'Token'}</th>}
              {(status === 'submitted' || status === 'ready') && <th>Action</th>}
            </tr>
          </thead>
          <tbody>
            {rows.map((prescription) => (
              <tr key={prescription.id}>
                <td>{new Date(prescription.created_at).toLocaleString()}</td>
                <td>
                  {prescription.patient_name}
                  <br />
                  <span className="muted">{prescription.patient_code}</span>
                </td>
                <td>{prescription.note ?? <span className="muted">No note.</span>}</td>
                <td>
                  <OpenFileButton prescription={prescription} />
                </td>
                {status !== 'submitted' && (
                  <td>
                    {status === 'rejected' ? (
                      prescription.rejection_reason
                    ) : (
                      <strong>{prescription.token_number}</strong>
                    )}
                  </td>
                )}
                {status === 'submitted' && (
                  <td>
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => ready.mutate({ path: { id: prescription.id } })}
                    >
                      Ready — issue token
                    </button>{' '}
                    <button
                      type="button"
                      className="secondary"
                      onClick={() => setRejecting(prescription)}
                    >
                      Can&rsquo;t fill
                    </button>
                  </td>
                )}
                {status === 'ready' && (
                  <td>
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => deliver.mutate({ path: { id: prescription.id } })}
                    >
                      Mark delivered
                    </button>{' '}
                    <button
                      type="button"
                      className="secondary"
                      onClick={() => setRejecting(prescription)}
                    >
                      Can&rsquo;t fill
                    </button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {rejecting && (
        <RejectDialog
          prescription={rejecting}
          onClose={() => setRejecting(null)}
          onDone={() => {
            setRejecting(null);
            refresh();
          }}
        />
      )}
    </div>
  );
}

function OpenFileButton({ prescription }: { prescription: Prescription }) {
  const [opening, setOpening] = useState(false);

  return (
    <button
      type="button"
      className="secondary"
      disabled={opening}
      onClick={async () => {
        setOpening(true);

        try {
          const { data, error } = await downloadPrescription({ path: { id: prescription.id } });

          if (error || !data) {
            return;
          }

          const url = URL.createObjectURL(data as Blob);
          window.open(url, '_blank', 'noopener');

          // Held briefly rather than revoked immediately: revoking before the new tab has read
          // it gives a blank viewer.
          setTimeout(() => URL.revokeObjectURL(url), 60_000);
        } finally {
          setOpening(false);
        }
      }}
    >
      {opening ? 'Opening…' : 'View'}
    </button>
  );
}

function RejectDialog({
  prescription,
  onClose,
  onDone,
}: {
  prescription: Prescription;
  onClose: () => void;
  onDone: () => void;
}) {
  const [reason, setReason] = useState('');

  const reject = useMutation({
    ...rejectPrescriptionMutation(),
    onSuccess: () => {
      toast.success(`${prescription.patient_name} will see why in the app.`);
      onDone();
    },
  });

  return (
    <Dialog title={`Can't fill ${prescription.patient_name}'s prescription`} onClose={onClose}>
      <p className="muted">The patient sees this reason in the app, so say what they should do next.</p>

      <form
        onSubmit={(event: FormEvent) => {
          event.preventDefault();
          reject.mutate({ path: { id: prescription.id }, body: { reason: reason.trim() } });
        }}
      >
        <label htmlFor="reject-reason">Reason</label>
        <textarea
          id="reject-reason"
          rows={3}
          maxLength={500}
          value={reason}
          placeholder="The photo is blurred. Please send a clearer one."
          onChange={(event) => setReason(event.target.value)}
          required
        />

        <div className="actions">
          <button type="submit" disabled={reject.isPending || reason.trim().length === 0}>
            {reject.isPending ? 'Sending…' : "Can't fill"}
          </button>
          <button type="button" className="secondary" onClick={onClose}>
            Cancel
          </button>
        </div>
      </form>
    </Dialog>
  );
}
