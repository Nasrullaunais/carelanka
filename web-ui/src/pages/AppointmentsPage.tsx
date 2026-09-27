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
  createPatientMutation,
  createWalkInAppointmentMutation,
  getPatientOptions,
  listAppointmentsOptions,
  listPatientsOptions,
  markAppointmentNoShowMutation,
  updatePatientMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type {
  Admission,
  Appointment,
  AppointmentStatus,
  Gender,
  Patient,
  PatientSummary,
  PrincipalRole,
} from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { BillPanel } from '../components/BillPanel';
import { ActionDialog } from '../components/ui/action-dialog';
import { ConfirmDialog } from '../components/ui/confirm-dialog';
import { AppSelect } from '../components/ui/app-select';
import {
  canBillAppointment,
  canChangePatientIdentity,
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
  genderLabels,
  patientIdentifier,
  selectableGenders,
} from '../types/patients';
import { fieldLimits, nicProblem, phoneProblem } from '../types/identifiers';
import {
  PatientFields,
  emptyPatientForm,
  patientFormBody,
  patientFormFrom,
  patientFormProblems,
  usePatientForm,
} from './intake-form';

const PAGE_SIZE = 20;

export function AppointmentsPage() {
  const session = useSession();
  const role = session?.principal.role;

  const [date, setDate] = useState(() => localDay(new Date()));
  const [status, setStatus] = useState<AppointmentStatus | ''>('');
  const [search, setSearch] = useState('');
  const [submittedSearch, setSubmittedSearch] = useState('');
  const [includeFinished, setIncludeFinished] = useState(false);
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
        ...(submittedSearch ? { search: submittedSearch } : {}),
        includeFinished,
        page,
        pageSize: PAGE_SIZE,
      },
    }),
    enabled: canSee,
  });

  if (!canSee) {
    return (
      <>
        <h1>Appointments</h1>
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

  function submitSearch(event: FormEvent) {
    event.preventDefault();
    resetTo(() => setSubmittedSearch(search.trim()));
  }

  return (
    <>
      <h1>Appointments</h1>
      <p className="muted">
        Patients booked to come in, and walk-ins recorded at the desk. Confirm a booking when
        you read it; the actions for the day itself open up once it is confirmed.
      </p>

      <div className="card">
        <h2>Filter</h2>
        <form onSubmit={submitSearch}>
          <div className="row">
            <div className="field">
              <label htmlFor="filter-search">Search</label>
              <input
                id="filter-search"
                value={search}
                maxLength={100}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Patient name, NIC, phone or code"
              />
            </div>
            <div style={{ display: 'flex', alignItems: 'flex-end', marginBottom: '0.85rem' }}>
              <button type="submit">Search</button>
            </div>
            <div style={{ display: 'flex', alignItems: 'flex-end', marginBottom: '0.85rem' }}>
              <button
                type="button"
                className="secondary"
                disabled={search === '' && submittedSearch === ''}
                onClick={() => resetTo(() => {
                  setSearch('');
                  setSubmittedSearch('');
                })}
              >
                Clear
              </button>
            </div>
          </div>

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

          <div className="field">
            <label htmlFor="filter-finished">
              <input
                id="filter-finished"
                type="checkbox"
                checked={includeFinished || status !== ''}
                disabled={status !== ''}
                onChange={(event) => resetTo(() => setIncludeFinished(event.target.checked))}
              />{' '}
              Show finished visits
            </label>
            <p className="hint">
              {status !== ''
                ? 'A chosen status already shows finished visits when it is one of them.'
                : 'Off by default: seen, cancelled and missed bookings are left off the list.'}
            </p>
          </div>
        </form>
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
                role={role}
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
              <td>
                <div className="row" style={{ gap: '0.4rem', justifyContent: 'flex-end' }}>
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
                </div>
              </td>
            </tr>

        ))}
      </tbody>
    </Table>
    <ActionDialog
      title={`${openAction === 'check-in' ? 'Check in' : openAction === 'confirm' ? 'Confirm' : openAction === 'cancel' ? 'Cancel' : 'Bill'} · ${activeAppointment?.patient.full_name ?? 'appointment'}`}
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

type BookingPatient = Pick<PatientSummary, 'id' | 'full_name' | 'nic' | 'temp_reference'>;

type Arrival = 'later' | 'now';

function BookVisitCard({ onBooked }: { onBooked: () => void }) {
  const queryClient = useQueryClient();

  const [search, setSearch] = useState('');
  const [submitted, setSubmitted] = useState('');
  const [registering, setRegistering] = useState(false);
  const [patient, setPatient] = useState<BookingPatient | null>(null);
  const [arrival, setArrival] = useState<Arrival>('later');
  const [when, setWhen] = useState('');
  const [reason, setReason] = useState('');

  const patients = useQuery({
    ...listPatientsOptions({ query: { search: submitted, pageSize: 5 } }),
    enabled: submitted.length > 0,
  });

  function finish() {
    queryClient.invalidateQueries({
      predicate: (query) => {
        const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;
        return id === 'listAppointments' || id === 'listPatientWorklist';
      },
    });

    setPatient(null);
    setSearch('');
    setSubmitted('');
    setArrival('later');
    setWhen('');
    setReason('');
    onBooked();
  }

  const book = useMutation({
    ...createAppointmentMutation(),
    onSuccess: (appointment) => {
      toast.success(
        `${appointment.patient.full_name} booked for ${localDateTime(appointment.scheduled_at)}.`,
      );
      finish();
    },
  });

  const walkIn = useMutation({
    ...createWalkInAppointmentMutation(),
    onSuccess: (appointment) => {
      toast.success(
        `${appointment.patient.full_name} is recorded as here now. Check them in from today's list.`,
      );
      finish();
    },
  });

  function choose(chosen: BookingPatient) {
    setPatient(chosen);
    setRegistering(false);
    setWhen(localInputValue(new Date(Date.now() + 60 * 60 * 1000)));
  }

  const isFuture = when !== '' && new Date(when).getTime() > Date.now();
  const cleanReason = reason.trim().length > 0 ? reason.trim() : null;
  const pending = book.isPending || walkIn.isPending;

  function submit(event: FormEvent) {
    event.preventDefault();
    if (!patient) return;

    if (arrival === 'now') {
      walkIn.mutate({ body: { patient_id: patient.id, reason: cleanReason } });
      return;
    }

    if (!isFuture) return;

    book.mutate({
      body: {
        patient_id: patient.id,
        scheduled_at: new Date(when).toISOString(),
        reason: cleanReason,
      },
    });
  }

  if (registering) {
    return (
      <RegisterForBooking
        search={submitted}
        onRegistered={choose}
        onBack={() => setRegistering(false)}
      />
    );
  }

  const searched = submitted !== '' && !patients.isFetching && patients.data !== undefined;
  const matches = patients.data?.items ?? [];

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

          {searched && matches.length === 0 && (
            <p className="empty">
              No patient matches &ldquo;{submitted}&rdquo;. If this is their first visit, register
              them below.
            </p>
          )}

          {searched && matches.length > 0 && (
            <Table>
              <tbody>
                {matches.map((found) => (
                  <tr key={found.id}>
                    <td>
                      <strong>{found.full_name}</strong>
                      <br />
                      <span className="muted">
                        {patientIdentifier(found) ?? 'No NIC on record'}
                      </span>
                    </td>
                    <td style={{ textAlign: 'right' }}>
                      <button type="button" className="secondary" onClick={() => choose(found)}>
                        Book a visit
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </Table>
          )}

          {searched && (
            <p style={{ marginTop: '0.9rem' }}>
              <button
                type="button"
                className={matches.length === 0 ? undefined : 'secondary'}
                onClick={() => setRegistering(true)}
              >
                {matches.length === 0
                  ? 'Register a new patient'
                  : 'None of these - register a new patient'}
              </button>
            </p>
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
              onClick={() => {
                setPatient(null);
                setSearch('');
                setSubmitted('');
                setReason('');
              }}
              style={{ marginLeft: '0.4rem' }}
            >
              Someone else
            </button>
          </p>

          <div className="field">
            <AppSelect
              id="book-arrival"
              label="When are they coming?"
              value={arrival}
              onValueChange={(value) => setArrival(value as Arrival)}
              options={[
                { value: 'later', label: 'Book a date and time' },
                { value: 'now', label: 'They are here now (walk-in)' },
              ]}
            />
            {arrival === 'now' && (
              <p className="hint">
                Recorded for right now and already confirmed, so you can check them in straight
                away from today&rsquo;s list.
              </p>
            )}
          </div>

          <div className="row">
            {arrival === 'later' && (
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
                    That time has already passed. For someone at the counter now, choose
                    &ldquo;They are here now&rdquo; above.
                  </p>
                )}
              </div>
            )}
            <div className="field">
              <label htmlFor="book-reason">Reason (optional)</label>
              <input
                id="book-reason"
                value={reason}
                maxLength={300}
                onChange={(event) => setReason(event.target.value)}
                placeholder="Blood test, chest X-ray, follow-up"
              />
            </div>
          </div>

          <button type="submit" disabled={pending || (arrival === 'later' && !isFuture)}>
            {pending ? 'Saving…' : arrival === 'now' ? 'Record the walk-in' : 'Book the visit'}
          </button>
        </form>
      )}
    </div>
  );
}

function prefillFromSearch(search: string) {
  const text = search.trim();

  if (text.length > 0 && phoneProblem(text) === null) {
    return { fullName: '', phone: text, nic: '' };
  }

  if (/\d/.test(text) && nicProblem(text) === null) {
    return { fullName: '', phone: '', nic: text };
  }

  return { fullName: text, phone: '', nic: '' };
}

/// A short record on purpose: the patient is often on the phone, and a test or scan needs no
/// more. The full intake details are taken at check-in, only if they are admitted to a ward.
function RegisterForBooking({
  search,
  onRegistered,
  onBack,
}: {
  search: string;
  onRegistered: (patient: Patient) => void;
  onBack: () => void;
}) {
  const queryClient = useQueryClient();

  const [initial] = useState(() => prefillFromSearch(search));
  const [fullName, setFullName] = useState(initial.fullName);
  const [phone, setPhone] = useState(initial.phone);
  const [gender, setGender] = useState<Gender | ''>('');
  const [nic, setNic] = useState(initial.nic);

  const phoneError = phoneProblem(phone);
  const nicError = nicProblem(nic);

  const blocked =
    fullName.trim().length === 0 ||
    phone.trim().length === 0 ||
    phoneError !== null ||
    gender === '' ||
    nicError !== null;

  const register = useMutation({
    ...createPatientMutation(),
    onSuccess: (created) => {
      toast.success(`${created.full_name} registered. Patient ID ${created.patient_code}.`);

      queryClient.invalidateQueries({
        predicate: (query) =>
          (query.queryKey[0] as { _id?: string } | undefined)?._id === 'listPatients',
      });

      onRegistered(created);
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    if (gender === '' || blocked) return;

    register.mutate({
      body: {
        full_name: fullName.trim(),
        nic: nic.trim().length > 0 ? nic.trim() : null,
        gender,
        date_of_birth: null,
        phone: phone.trim(),
        address: null,
        emergency_contact_name: null,
        emergency_contact_phone: null,
      },
    });
  }

  return (
    <div className="table-section">
      <h2>Register a new patient</h2>
      <p className="muted" style={{ marginBottom: '0.9rem' }}>
        Only what is needed to book the visit. If they are admitted to a ward when they arrive,
        the rest of their details are taken at check-in.
      </p>

      <form onSubmit={submit}>
        <div className="row">
          <div className="field">
            <label htmlFor="new-name">Full name</label>
            <input
              id="new-name"
              value={fullName}
              maxLength={fieldLimits.fullName}
              onChange={(event) => setFullName(event.target.value)}
              required
              autoFocus
            />
          </div>
          <div className="field">
            <AppSelect
              id="new-gender"
              label="Gender"
              value={gender}
              onValueChange={(value) => setGender(value as Gender | '')}
              options={[
                { value: '', label: 'Choose' },
                ...selectableGenders.map((value) => ({ value, label: genderLabels[value] })),
              ]}
            />
          </div>
        </div>

        <div className="row">
          <div className="field">
            <label htmlFor="new-phone">Mobile number</label>
            <input
              id="new-phone"
              value={phone}
              inputMode="tel"
              maxLength={20}
              placeholder="0771234567"
              aria-invalid={phoneError !== null}
              onChange={(event) => setPhone(event.target.value)}
              required
            />
            {phoneError && <p className="field-error">{phoneError}</p>}
          </div>
          <div className="field">
            <label htmlFor="new-nic">
              NIC <span className="muted">(optional)</span>
            </label>
            <input
              id="new-nic"
              value={nic}
              maxLength={20}
              placeholder="199534501V"
              aria-invalid={nicError !== null}
              onChange={(event) => setNic(event.target.value)}
            />
            {nicError && <p className="field-error">{nicError}</p>}
            <p className="hint">
              The NIC lets the patient connect this record to the CareLanka app, where they can
              see their visits and bills. It also stops the same person being registered twice.
            </p>
          </div>
        </div>

        <div className="row">
          <button type="submit" disabled={register.isPending || blocked}>
            {register.isPending ? 'Registering…' : 'Register and continue'}
          </button>
          <button type="button" className="secondary" onClick={onBack}>
            Back to search
          </button>
        </div>
      </form>
    </div>
  );
}

/// A patient registered while booking has only a name, phone and gender. Walk-in intake takes
/// these three from anyone who can answer, so an admission asks for them before it goes ahead.
function lacksIntakeDetails(patient: Patient): boolean {
  return !patient.date_of_birth || !patient.address || !patient.phone;
}

function CheckInPanel({
  appointment,
  role,
  noBedPending,
  onCancel,
  onNoBed,
  onCheckedIn,
}: {
  appointment: Appointment;
  role: PrincipalRole | undefined;
  noBedPending: boolean;
  onCancel: () => void;
  onNoBed: () => void;
  onCheckedIn: (admission: Admission) => void;
}) {
  const queryClient = useQueryClient();

  const [choice, setChoice] = useState<CheckInChoice>('general');
  const [isInfectious, setIsInfectious] = useState(false);

  const admitting = choice !== 'no_bed';

  const details = useQuery({
    ...getPatientOptions({ path: { id: appointment.patient.id } }),
    enabled: admitting,
  });

  const form = usePatientForm(emptyPatientForm(false));
  const [nicInput, setNicInput] = useState('');
  const [loadedId, setLoadedId] = useState<string | null>(null);

  if (details.data && loadedId !== details.data.id) {
    setLoadedId(details.data.id);
    form.replace({
      ...patientFormFrom(details.data),
      ...(choice === 'maternity' ? { gender: 'female' as const } : {}),
    });
    setNicInput(details.data.nic ?? '');
  }

  // Walk-in intake lets an emergency skip the full form, so check-in does too.
  const needsDetails =
    admitting &&
    choice !== 'emergency' &&
    details.data !== undefined &&
    lacksIntakeDetails(details.data);
  const problems = patientFormProblems(form.value, true, nicInput);
  const canChangeIdentity = canChangePatientIdentity(role, !!details.data?.nic);
  const currentNicProblem = nicProblem(nicInput);

  const saveDetails = useMutation({
    ...updatePatientMutation(),
    onSuccess: () => {
      queryClient.invalidateQueries({
        predicate: (query) => {
          const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;
          return id === 'listPatients' || id === 'getPatient';
        },
      });
    },
  });

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

  function chooseLevel(next: CheckInChoice) {
    setChoice(next);

    if (next === 'maternity') {
      form.set('gender', 'female');
    } else if (details.data) {
      form.set('gender', details.data.gender);
    }
  }

  function admit(category: Exclude<CheckInChoice, 'no_bed'>) {
    checkIn.mutate({
      path: { id: appointment.id },
      body: {
        admission_category: category,
        urgency: 'routine',
        is_infectious: isInfectious,
      },
    });
  }

  function submit(event: FormEvent) {
    event.preventDefault();

    if (choice === 'no_bed') {
      onNoBed();
      return;
    }

    if (!needsDetails) {
      admit(choice);
      return;
    }

    if (problems.blocked || currentNicProblem !== null) return;

    saveDetails.mutate(
      {
        path: { id: appointment.patient.id },
        body: patientFormBody(
          form.value,
          canChangeIdentity ? nicInput.trim() || null : details.data?.nic ?? null,
        ),
      },
      { onSuccess: () => admit(choice) },
    );
  }

  const pending = checkIn.isPending || saveDetails.isPending || noBedPending;
  const waitingForDetails = admitting && (details.isLoading || details.isError);

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
            onValueChange={(value) => chooseLevel(value as CheckInChoice)}
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

        {admitting && details.isLoading && <p className="empty">Loading patient details…</p>}

        {admitting && details.isError && (
          <div className="empty">
            <p>Could not load the patient&rsquo;s details.</p>
            <button type="button" className="secondary" onClick={() => void details.refetch()}>
              Try again
            </button>
          </div>
        )}

        {needsDetails && (
          <>
            <p className="stub-note" style={{ marginBottom: '0.9rem' }}>
              <strong>Complete their details before admitting.</strong> This patient was
              registered with only a name, phone and gender. A ward admission needs the same
              details as walk-in intake.
            </p>
            {canChangeIdentity && (
              <div className="field">
                <label htmlFor="checkin-nic">NIC</label>
                <input
                  id="checkin-nic"
                  value={nicInput}
                  maxLength={20}
                  aria-invalid={currentNicProblem !== null}
                  onChange={(event) => setNicInput(event.target.value)}
                  placeholder="199534501V"
                />
                {currentNicProblem && <p className="field-error">{currentNicProblem}</p>}
              </div>
            )}
            <PatientFields
              value={form.value}
              set={form.set}
              idPrefix="checkin"
              identified
              lockGenderTo={choice === 'maternity' ? 'female' : undefined}
              identityLocked={!canChangeIdentity}
              nic={nicInput}
            />
          </>
        )}

        {admitting && (
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
          <button
            type="submit"
            disabled={pending || waitingForDetails || (needsDetails && problems.blocked)}
          >
            {pending
              ? 'Saving…'
              : choice === 'no_bed'
                ? 'Record the visit and bill'
                : needsDetails
                  ? 'Save details and admit'
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
