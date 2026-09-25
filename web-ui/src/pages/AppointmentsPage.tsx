import { PaginationControls } from '../components/ui/pagination-controls';
import { Table } from '../components/Table';
import { useState } from 'react';
import type { FormEvent, ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  cancelAppointmentAtTheDeskMutation,
  checkInAppointmentMutation,
  completeAppointmentMutation,
  confirmAppointmentMutation,
  createAppointmentMutation,
  listAppointmentsOptions,
  listPatientsOptions,
  markAppointmentNoShowMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type {
  Admission,
  Appointment,
  AppointmentStatus,
  PatientSummary,
} from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { BillPanel } from '../components/BillPanel';
import { ActionDialog } from '../components/ui/action-dialog';
import { ConfirmDialog } from '../components/ui/confirm-dialog';
import { AppSelect } from '../components/ui/app-select';
import {
  canBillAppointment,
  canOpenAppointmentBoard,
  canWorkAppointmentDesk,
} from '../types/permissions';
import {
  appointmentOutcome,
  appointmentStatusLabels,
  appointmentStatusTone,
  appointmentStatuses,
  noBedLabel,
  type CheckInChoice,
} from '../types/appointments';
import { hasPassed, localDateTime, localInputValue, localTime, localDay } from '../types/datetime';
import {
  admissionCategoriesFor,
  admissionCategoryHints,
  admissionCategoryLabels,
  detailFieldLabel,
  patientIdentifier,
} from '../types/patients';

const PAGE_SIZE = 20;

export function AppointmentsPage() {
  const session = useSession();
  const role = session?.principal.role;

  const [date, setDate] = useState(() => localDay(new Date()));
  const [status, setStatus] = useState<AppointmentStatus | ''>('');
  const [page, setPage] = useState(1);
  const queryClient = useQueryClient();
  const [open, setOpen] = useState<{ appointment: Appointment; action: DeskAction } | null>(null);
  const [admitted, setAdmitted] = useState<Admission | null>(null);
  const [noShowCandidate, setNoShowCandidate] = useState<Appointment | null>(null);
  const [bookingOpen, setBookingOpen] = useState(false);

  // Two different questions: who may see the list, and who may act on a
  // booking. The administrator can bill a finished visit but cannot check
  // anyone in.
  const canSee = canOpenAppointmentBoard(role);
  const isDesk = canWorkAppointmentDesk(role);
  const billing = canBillAppointment(role);

  const appointments = useQuery({
    ...listAppointmentsOptions({
      query: {
        ...(date ? { date } : {}),
        ...(status ? { status } : {}),
        page,
        pageSize: PAGE_SIZE,
      },
    }),
    enabled: canSee,
  });

  if (!canSee) {
    return (
      <>
        <h1>Expected visits</h1>
        <p className="empty">
          Your role cannot open the bookings list. Reception, ward nurses, the duty manager
          and the administrator can.
        </p>
      </>
    );
  }

  function resetTo(first: () => void) {
    first();
    setPage(1);
    setOpen(null);
  }

  function toggle(appointment: Appointment, action: DeskAction) {
    setAdmitted(null);
    setOpen((current) =>
      current?.appointment.id === appointment.id && current.action === action
        ? null
        : { appointment, action },
    );
  }

  const refreshBookings = () =>
    queryClient.invalidateQueries({
      predicate: (query) =>
        (query.queryKey[0] as { _id?: string } | undefined)?._id === 'listAppointments',
    });

  const noShow = useMutation({
    ...markAppointmentNoShowMutation(),
    onSuccess: (updated) => {
      toast.success(`${updated.patient.full_name} marked as not attended.`);
      void refreshBookings();
      setOpen(null);
      setNoShowCandidate(null);
    },
  });

  const complete = useMutation({
    ...completeAppointmentMutation(),
    onSuccess: (updated) => {
      toast.success('Visit recorded. Raise the bill below.');
      void refreshBookings();
      setAdmitted(null);
      setOpen({ appointment: updated, action: 'bill' });
    },
  });

  function markNotAttended(appointment: Appointment) {
    setNoShowCandidate(appointment);
  }

  return (
    <>
      <h1>Expected visits</h1>
      <p className="muted">
        Patients booked to come in, so the desk knows before they arrive. Confirm a booking
        when you read it; the actions for the day itself open up once it is confirmed.
      </p>

      <div className="card">
        <h2>Filter</h2>
        <div className="row">
          <div>
            <label htmlFor="filter-date">Day</label>
            <input
              id="filter-date"
              type="date"
              value={date}
              onChange={(event) => resetTo(() => setDate(event.target.value))}
            />
          </div>
          <div>
            <AppSelect
              id="filter-status"
              label="Status"
              value={status}
              onValueChange={(value) =>
                resetTo(() => setStatus(value as AppointmentStatus | ''))
              }
              options={[
                { value: '', label: 'Any status' },
                ...appointmentStatuses.map((value) => ({ value, label: appointmentStatusLabels[value] })),
              ]}
            />
          </div>
          <div style={{ display: 'flex', alignItems: 'flex-end' }}>
            <button
              type="button"
              className="secondary"
              onClick={() => resetTo(() => setDate(''))}
              disabled={date === ''}
            >
              All days
            </button>
          </div>
        </div>

      </div>

      {isDesk && <button type="button" onClick={() => setBookingOpen(true)}>Book a visit</button>}
      <ActionDialog title="Book a visit" isOpen={bookingOpen} onClose={() => setBookingOpen(false)}>
        {bookingOpen && <BookVisitCard onBooked={() => setBookingOpen(false)} />}
      </ActionDialog>

      {admitted && <CheckedInCard admission={admitted} onDismiss={() => setAdmitted(null)} />}

      <div className="table-section">
        <h2>
          {status ? appointmentStatusLabels[status] : 'All bookings'}
          {date ? '' : ' — every day'}
        </h2>

        <AppointmentTable
          footer={<PaginationControls label="Appointments" page={page} totalPages={appointments.data?.total_pages ?? 1} totalItems={appointments.data?.total_items} onPageChange={setPage} />}
          appointments={appointments.data?.items ?? []}
          isLoading={appointments.isLoading}
          isError={appointments.isError}
          onRetry={() => void appointments.refetch()}
          onAction={toggle}
          onCloseAction={() => setOpen(null)}
          onNotAttended={markNotAttended}
          canAct={isDesk}
          canBill={billing}
          showDate={date === ''}
          openId={open?.appointment.id ?? null}
          openAction={open?.action ?? null}
          activeAppointment={open?.appointment ?? null}
          renderDrawer={(appointment) => {
            if (open?.action === 'confirm') {
              return (
                <ConfirmPanel
                  appointment={appointment}
                  onDone={() => setOpen(null)}
                  onCancel={() => setOpen(null)}
                />
              );
            }

            if (open?.action === 'cancel') {
              return <CancelPanel appointment={appointment} onDone={() => setOpen(null)} />;
            }

            if (open?.action === 'bill') {
              return (
                <BillPanel appointmentId={appointment.id} canSettle={billing} />
              );
            }

            return (
              <CheckInPanel
                appointment={appointment}
                staffId={session?.principal.id ?? ''}
                noBedPending={complete.isPending}
                onCancel={() => setOpen(null)}
                onNoBed={() => complete.mutate({ path: { id: appointment.id } })}
                onCheckedIn={(admission) => {
                  setOpen(null);
                  setAdmitted(admission);
                }}
              />
            );
          }}
        />
        <ConfirmDialog
          isOpen={noShowCandidate != null}
          onOpenChange={(open) => { if (!open) setNoShowCandidate(null); }}
          title={`Mark ${noShowCandidate?.patient.full_name ?? 'patient'} as not attended?`}
          description="This closes the booking as a missed visit. Check that the booked time has passed and the patient did not arrive."
          confirmLabel={noShow.isPending ? 'Saving…' : 'Mark not attended'}
          isPending={noShow.isPending}
          onConfirm={() => { if (noShowCandidate) noShow.mutate({ path: { id: noShowCandidate.id } }); }}
        />


      </div>
    </>
  );
}

type DeskAction = 'confirm' | 'check-in' | 'cancel' | 'bill';

function AppointmentTable({
  footer,
  appointments,
  isLoading,
  isError,
  onRetry,
  onAction,
  onCloseAction,
  onNotAttended,
  canAct,
  canBill,
  showDate,
  openId,
  openAction,
  activeAppointment,
  renderDrawer,
}: {
  footer?: React.ReactNode;
  appointments: Appointment[];
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  onAction: (appointment: Appointment, action: DeskAction) => void;
  onCloseAction: () => void;
  onNotAttended: (appointment: Appointment) => void;
  canAct: boolean;
  canBill: boolean;
  showDate: boolean;
  openId: string | null;
  openAction: DeskAction | null;
  activeAppointment: Appointment | null;
  renderDrawer: (appointment: Appointment) => ReactNode;
}) {
  if (isLoading) {
    return <p className="empty">Loading…</p>;
  }

  if (isError) {
    return (
      <div className="empty">
        <p>Could not load the appointments.</p>
        <button type="button" className="secondary" onClick={onRetry}>
          Try again
        </button>
      </div>
    );
  }

  if (appointments.length === 0) {
    return <p className="empty">No appointments match this filter.</p>;
  }

  return (
    <>
    <Table footer={footer}>
      <thead>
        <tr>
          <th>{showDate ? 'When' : 'Time'}</th>
          <th>Patient</th>
          <th>Reason</th>
          <th>Booked</th>
          <th>Status</th>
          <th />
        </tr>
      </thead>
      <tbody>
        {appointments.map((appointment) => (
            <tr key={appointment.id} className={openId === appointment.id ? 'open' : undefined}>
              <td>
                <strong>
                  {showDate
                    ? localDateTime(appointment.scheduled_at)
                    : localTime(appointment.scheduled_at)}
                </strong>
              </td>
              <td>
                {appointment.patient.full_name}
                <br />
                <span className="muted">
                  {patientIdentifier(appointment.patient) ?? 'No NIC on record'}
                </span>
              </td>
              <td>
                {appointment.reason ?? <span className="muted">Not given</span>}
                {appointment.cancellation_reason && (
                  <>
                    <br />
                    <span className="muted">
                      Called off: {appointment.cancellation_reason}
                    </span>
                  </>
                )}
              </td>
              <td className="nowrap">
                {appointment.booked_by_staff_id ? 'At the desk' : 'In the app'}
              </td>
              <td>
                <span className={appointmentStatusTone(appointment.status)}>
                  {appointmentStatusLabels[appointment.status]}
                </span>
                {appointmentOutcome(appointment) && (
                  <>
                    <br />
                    <span className="muted">{appointmentOutcome(appointment)}</span>
                  </>
                )}
              </td>
              <td className="row" style={{ gap: '0.4rem', justifyContent: 'flex-end' }}>
                {/* Nothing on the day is offered until the desk has read the booking, so a
                    visit weeks away can never start a bed search by mistake. */}
                {canAct && appointment.can_confirm && (
                  <button type="button" onClick={() => onAction(appointment, 'confirm')}>
                    {openId === appointment.id && openAction === 'confirm'
                      ? 'Close'
                      : 'Check and confirm'}
                  </button>
                )}

                {canAct && appointment.can_complete && (
                  <button type="button" onClick={() => onAction(appointment, 'check-in')}>
                    {openId === appointment.id && openAction === 'check-in'
                      ? 'Close'
                      : 'Check in'}
                  </button>
                )}

                {canAct && appointment.can_complete && (
                  <button
                    type="button"
                    className="secondary"
                    disabled={!hasPassed(appointment.scheduled_at)}
                    title={
                      hasPassed(appointment.scheduled_at)
                        ? undefined
                        : 'Not yet — this can only be marked once the booked time has passed.'
                    }
                    onClick={() => onNotAttended(appointment)}
                  >
                    Did not come
                  </button>
                )}

                {canAct && appointment.can_cancel && (
                  <button
                    type="button"
                    className="secondary"
                    disabled={hasPassed(appointment.scheduled_at)}
                    title={
                      hasPassed(appointment.scheduled_at)
                        ? 'The booked time has passed — use Did not come or Check in instead.'
                        : undefined
                    }
                    onClick={() => onAction(appointment, 'cancel')}
                  >
                    {openId === appointment.id && openAction === 'cancel'
                      ? 'Close'
                      : 'Cancel booking'}
                  </button>
                )}

                {/* An admitted patient is billed on their admission at discharge, so this
                    row has no bill of its own. */}
                {canBill && appointment.status === 'completed' && !appointment.admission_id && (
                  <button
                    type="button"
                    className="secondary"
                    onClick={() => onAction(appointment, 'bill')}
                  >
                    {openId === appointment.id && openAction === 'bill' ? 'Close' : 'Bill'}
                  </button>
                )}

                {appointment.status === 'completed' && appointment.admission_id && (
                  <Link to="/patients" className="muted" style={{ fontSize: '0.82rem' }}>
                    On the ward board
                  </Link>
                )}
              </td>
            </tr>

        ))}
      </tbody>
    </Table>
    <ActionDialog
      title={`${openAction === 'check-in' ? 'Admit' : openAction === 'confirm' ? 'Confirm' : openAction === 'cancel' ? 'Cancel' : 'Bill'} · ${activeAppointment?.patient.full_name ?? 'appointment'}`}
      isOpen={openId != null}
      onClose={onCloseAction}
    >
      {activeAppointment && renderDrawer(activeAppointment)}
    </ActionDialog>
    </>
  );
}

/// Reading the booking before committing to it. The desk needs to see what the
/// patient actually asked for — the time and their own words — before it is
/// marked expected, not just the patient's name.
function ConfirmPanel({
  appointment,
  onDone,
  onCancel,
}: {
  appointment: Appointment;
  onDone: () => void;
  onCancel: () => void;
}) {
  const queryClient = useQueryClient();

  const confirm = useMutation({
    ...confirmAppointmentMutation(),
    onSuccess: (updated) => {
      toast.success(`${updated.patient.full_name}'s visit is confirmed.`);
      void queryClient.invalidateQueries({
        predicate: (query) =>
          (query.queryKey[0] as { _id?: string } | undefined)?._id === 'listAppointments',
      });
      onDone();
    },
  });

  return (
    <div className="drawer-body">
      <h3>Confirm {appointment.patient.full_name}&apos;s booking</h3>
      <p style={{ marginBottom: '0.9rem' }}>
        <strong>{localDateTime(appointment.scheduled_at)}</strong>
        <br />
        {appointment.reason ? (
          appointment.reason
        ) : (
          <span className="muted">No reason given.</span>
        )}
      </p>
      <p className="muted" style={{ marginBottom: '0.9rem' }}>
        Confirming tells the patient the desk has read this and expects them at this time. If
        the time does not work, cancel the booking instead so the patient can rebook.
      </p>

      <div className="row">
        <button
          type="button"
          disabled={confirm.isPending}
          onClick={() => confirm.mutate({ path: { id: appointment.id } })}
        >
          {confirm.isPending ? 'Confirming…' : 'Confirm booking'}
        </button>
        <button type="button" className="secondary" onClick={onCancel}>
          Not yet
        </button>
      </div>
    </div>
  );
}

/// Calling a booking off from the desk. The reason is required because the
/// patient reads it in their app, and it is where the desk names a time the
/// clinic can actually see them.
function CancelPanel({
  appointment,
  onDone,
}: {
  appointment: Appointment;
  onDone: () => void;
}) {
  const queryClient = useQueryClient();
  const [reason, setReason] = useState('');

  const cancel = useMutation({
    ...cancelAppointmentAtTheDeskMutation(),
    onSuccess: () => {
      toast.success('Booking cancelled. The patient can see the reason in their app.');
      void queryClient.invalidateQueries({
        predicate: (query) =>
          (query.queryKey[0] as { _id?: string } | undefined)?._id === 'listAppointments',
      });
      onDone();
    },
  });

  return (
    <form
      onSubmit={(event: FormEvent) => {
        event.preventDefault();
        cancel.mutate({ path: { id: appointment.id }, body: { reason: reason.trim() } });
      }}
    >
      <h3>Cancel {appointment.patient.full_name}&apos;s booking</h3>
      <p className="muted">
        {localDateTime(appointment.scheduled_at)}. The patient reads this reason word for word,
        so name a time the clinic can see them if there is one.
      </p>

      <div>
        <label htmlFor="cancel-reason">Reason</label>
        <textarea
          id="cancel-reason"
          rows={3}
          value={reason}
          maxLength={300}
          required
          placeholder="Clinic full at 09:00. Please rebook after 14:00."
          onChange={(event) => setReason(event.target.value)}
        />
      </div>

      <div className="row" style={{ marginTop: '0.9rem' }}>
        <button type="submit" disabled={cancel.isPending || reason.trim().length === 0}>
          {cancel.isPending ? 'Cancelling…' : 'Cancel this booking'}
        </button>
        <button type="button" className="secondary" onClick={onDone}>
          Keep it
        </button>
      </div>
    </form>
  );
}

function BookVisitCard({ onBooked }: { onBooked: () => void }) {
  const queryClient = useQueryClient();

  const [search, setSearch] = useState('');
  const [submitted, setSubmitted] = useState('');
  const [patient, setPatient] = useState<PatientSummary | null>(null);
  const [when, setWhen] = useState('');
  const [reason, setReason] = useState('');

  const patients = useQuery({
    ...listPatientsOptions({ query: { search: submitted, pageSize: 5 } }),
    enabled: submitted.length > 0,
  });

  const book = useMutation({
    ...createAppointmentMutation(),
    onSuccess: (appointment) => {
      toast.success(
        `${appointment.patient.full_name} booked for ${localDateTime(appointment.scheduled_at)}.`,
      );

      queryClient.invalidateQueries({
        predicate: (query) => {
          const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;
          return id === 'listAppointments' || id === 'listPatientWorklist';
        },
      });

      setPatient(null);
      setSearch('');
      setSubmitted('');
      setWhen('');
      setReason('');
      onBooked();
    },
  });

  const isFuture = when !== '' && new Date(when).getTime() > Date.now();

  function submit(event: FormEvent) {
    event.preventDefault();
    if (!patient || !isFuture) return;

    book.mutate({
      body: {
        patient_id: patient.id,
        scheduled_at: new Date(when).toISOString(),
        reason: reason.trim().length > 0 ? reason.trim() : null,
      },
    });
  }

  return (
    <div className="table-section">
      <h2>Book a visit</h2>
      <p className="muted" style={{ marginBottom: '0.9rem' }}>
        For a patient on the phone or at the counter. A patient booking in the app uses the same
        rules: one open appointment each, and none for a patient already admitted.
      </p>

      {patient === null ? (
        <>
          <form
            onSubmit={(event) => {
              event.preventDefault();
              setSubmitted(search.trim());
            }}
          >
            <div className="row">
              <div className="field">
                <label htmlFor="book-search">Find the patient</label>
                <input
                  id="book-search"
                  value={search}
                  maxLength={100}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Name, NIC, phone or reference"
                />
              </div>
              <div style={{ display: 'flex', alignItems: 'flex-end', marginBottom: '0.85rem' }}>
                <button type="submit" disabled={search.trim().length === 0}>
                  Search
                </button>
              </div>
            </div>
          </form>

          {patients.isFetching && <p className="empty">Searching…</p>}

          {submitted !== '' && !patients.isFetching && patients.data?.items.length === 0 && (
            <p className="empty">
              No patient matches “{submitted}”. A patient must be registered before a visit can
              be booked — register them on the intake screen first.
            </p>
          )}

          {!patients.isFetching && (patients.data?.items.length ?? 0) > 0 && (
            <Table>
              <tbody>
                {patients.data?.items.map((found) => (
                  <tr key={found.id}>
                    <td>
                      <strong>{found.full_name}</strong>
                      <br />
                      <span className="muted">
                        {patientIdentifier(found) ?? 'No NIC on record'}
                      </span>
                    </td>
                    <td style={{ textAlign: 'right' }}>
                      <button
                        type="button"
                        className="secondary"
                        onClick={() => {
                          setPatient(found);
                          setWhen(localInputValue(new Date(Date.now() + 60 * 60 * 1000)));
                        }}
                      >
                        Book a visit
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </Table>
          )}
        </>
      ) : (
        <form onSubmit={submit}>
          <p style={{ margin: '0 0 0.85rem' }}>
            Booking for <strong>{patient.full_name}</strong>{' '}
            <span className="muted">({patientIdentifier(patient) ?? 'no NIC on record'})</span>{' '}
            <button
              type="button"
              className="secondary"
              onClick={() => setPatient(null)}
              style={{ marginLeft: '0.4rem' }}
            >
              Someone else
            </button>
          </p>

          <div className="row">
            <div className="field">
              <label htmlFor="book-when">Date and time</label>
              <input
                id="book-when"
                type="datetime-local"
                value={when}
                min={localInputValue(new Date())}
                onChange={(event) => setWhen(event.target.value)}
                required
              />
              {when !== '' && !isFuture && (
                <p className="hint">
                  That time has already passed. A patient who is here now is admitted, not
                  booked — use the intake screen instead.
                </p>
              )}
            </div>
            <div className="field">
              <label htmlFor="book-reason">Reason (optional)</label>
              <input
                id="book-reason"
                value={reason}
                maxLength={500}
                onChange={(event) => setReason(event.target.value)}
                placeholder="Follow-up, cardiology"
              />
              <p className="hint">
                For staff to read. The bed agent never reads it — free text stays data, never
                instructions.
              </p>
            </div>
          </div>

          <button type="submit" disabled={book.isPending || !isFuture}>
            {book.isPending ? 'Booking…' : 'Book the visit'}
          </button>
        </form>
      )}
    </div>
  );
}

function CheckInPanel({
  appointment,
  staffId,
  noBedPending,
  onCancel,
  onNoBed,
  onCheckedIn,
}: {
  appointment: Appointment;
  staffId: string;
  noBedPending: boolean;
  onCancel: () => void;
  onNoBed: () => void;
  onCheckedIn: (admission: Admission) => void;
}) {
  const queryClient = useQueryClient();

  const [choice, setChoice] = useState<CheckInChoice>('general');
  const [isInfectious, setIsInfectious] = useState(false);

  const checkIn = useMutation({
    ...checkInAppointmentMutation(),
    onSuccess: (admission) => {
      toast.success(`${appointment.patient.full_name} admitted.`);

      queryClient.invalidateQueries({
        predicate: (query) => {
          const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;
          return (
            id === 'listAppointments' ||
            id === 'listAdmissions' ||
            id === 'listPatientWorklist'
          );
        },
      });

      onCheckedIn(admission);
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();

    if (choice === 'no_bed') {
      onNoBed();
      return;
    }

    checkIn.mutate({
      path: { id: appointment.id },
      body: {
        admission_category: choice,
        category_set_by_staff_id: staffId,
        urgency: 'routine',
        is_infectious: isInfectious,
      },
    });
  }

  const pending = checkIn.isPending || noBedPending;

  return (
    <div className="drawer-body">
      <h3>Check in {appointment.patient.full_name}</h3>
      <p className="muted" style={{ marginBottom: '0.9rem' }}>
        Booked for {localDateTime(appointment.scheduled_at)}
        {appointment.reason ? ` — ${appointment.reason}` : ''}. Only do this with the patient
        in front of you.
      </p>

      <form onSubmit={submit}>
        <div className="field">
          <AppSelect
            id="checkin-category"
            label="What do they need?"
            value={choice}
            onValueChange={(value) => setChoice(value as CheckInChoice)}
            options={[
              ...admissionCategoriesFor(appointment.patient.gender).map((value) => ({
                value,
                label: admissionCategoryLabels[value],
              })),
              { value: 'no_bed', label: noBedLabel },
            ]}
          />
          <p className="hint">
            {choice === 'no_bed'
              ? 'The visit is recorded as finished and billed here, on this booking.'
              : `${admissionCategoryHints[choice]} They move to the Patients page to be given a bed and marked as arrived, the same as a walk-in. This is your decision and is recorded against your name.`}
          </p>
        </div>

        {choice !== 'no_bed' && (
          <div className="field">
            <label htmlFor="checkin-infectious">
              <input
                id="checkin-infectious"
                type="checkbox"
                checked={isInfectious}
                onChange={(event) => setIsInfectious(event.target.checked)}
              />{' '}
              Needs isolation
            </label>
            <p className="hint">Forces an isolation-capable bed when a bed is assigned.</p>
          </div>
        )}

        <div className="row">
          <button type="submit" disabled={pending}>
            {pending
              ? 'Saving…'
              : choice === 'no_bed'
                ? 'Record the visit and bill'
                : 'Admit to a ward'}
          </button>
          <button type="button" className="secondary" onClick={onCancel}>
            Cancel
          </button>
        </div>
      </form>
    </div>
  );
}

function CheckedInCard({
  admission,
  onDismiss,
}: {
  admission: Admission;
  onDismiss: () => void;
}) {
  return (
    <div className="card">
      <h2>Admitted</h2>
      <p className="muted">
        {admission.patient?.full_name ?? 'The patient'} is admitted and awaiting a bed. Care
        level:{' '}
        <strong>
          {admission.admission_category
            ? admissionCategoryLabels[admission.admission_category]
            : 'Not yet classified'}
        </strong>
        .
      </p>

      {admission.missing_fields.length > 0 && (
        <>
          <p style={{ marginTop: '0.9rem' }}>
            <strong>Details still missing:</strong>
          </p>
          <ul>
            {admission.missing_fields.map((field) => (
              <li key={field}>{detailFieldLabel(field)}</li>
            ))}
          </ul>
          <p className="muted">
            Missing details do not block the admission. This is a list to follow up, not a gate.
          </p>
        </>
      )}

      <button
        type="button"
        className="secondary"
        style={{ marginTop: '0.9rem' }}
        onClick={onDismiss}
      >
        Back to the list
      </button>
    </div>
  );
}
