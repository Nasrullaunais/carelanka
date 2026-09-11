import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  checkInAppointmentMutation,
  createAppointmentMutation,
  listAppointmentsOptions,
  listPatientsOptions,
} from '../services/api/generated/@tanstack/react-query.gen';
import type {
  Admission,
  AdmissionCategory,
  AdmissionUrgency,
  Appointment,
  AppointmentStatus,
  PatientSummary,
} from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { canSetHighCareLevel, canWorkAppointmentDesk } from '../types/permissions';
import {
  appointmentStatusLabels,
  appointmentStatuses,
  deskCareLevels,
  dutyManagerCareLevels,
} from '../types/appointments';
import { localDateTime, localInputValue, localTime, utcDay } from '../types/datetime';
import {
  admissionCategoryHints,
  admissionCategoryLabels,
  admissionUrgencies,
  admissionUrgencyLabels,
  detailFieldLabel,
  patientIdentifier,
} from '../types/patients';

// The expected-visits desk. Three jobs on one screen, in the order the day runs:
//
//   who is coming  ->  book someone in  ->  check them in when they walk up
//
// Check-in is the one that matters: it turns a booking into a real admission, and the care
// level is chosen HERE by the person at the desk. Not by the patient when they booked, and
// not by an agent.

const PAGE_SIZE = 20;

export function AppointmentsPage() {
  const session = useSession();
  const role = session?.principal.role;

  const [date, setDate] = useState(() => utcDay(new Date()));
  const [status, setStatus] = useState<AppointmentStatus | ''>('scheduled');
  const [page, setPage] = useState(1);
  const [checkingIn, setCheckingIn] = useState<Appointment | null>(null);
  const [admitted, setAdmitted] = useState<Admission | null>(null);

  // Gated rather than skipped: hooks cannot go behind the early return below, and without
  // this a doctor opening the URL fires a request that comes back 403 and toasts red — on a
  // page that is already explaining, calmly, that it is not theirs.
  const isDesk = canWorkAppointmentDesk(role);

  const appointments = useQuery({
    ...listAppointmentsOptions({
      query: {
        ...(date ? { date } : {}),
        ...(status ? { status } : {}),
        page,
        pageSize: PAGE_SIZE,
      },
    }),
    enabled: isDesk,
  });

  if (!isDesk) {
    return (
      <>
        <h1>Expected visits</h1>
        <p className="empty">
          Your role cannot work the bookings desk. Ward nurses and the duty manager can.
        </p>
      </>
    );
  }

  function resetTo(first: () => void) {
    first();
    setPage(1);
    setCheckingIn(null);
  }

  return (
    <>
      <h1>Expected visits</h1>
      <p className="muted">
        Who has booked to come in, so the desk knows before they walk up. Checking someone in
        turns their booking into an admission and starts the search for a bed.
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
            <label htmlFor="filter-status">Status</label>
            <select
              id="filter-status"
              value={status}
              onChange={(event) =>
                resetTo(() => setStatus(event.target.value as AppointmentStatus | ''))
              }
            >
              <option value="">Any status</option>
              {appointmentStatuses.map((value) => (
                <option key={value} value={value}>
                  {appointmentStatusLabels[value]}
                </option>
              ))}
            </select>
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

        {/* Worth saying once, plainly: the day here is not quite the day outside the window.
            Only when a day is actually chosen — with the filter off it explains nothing. */}
        {date !== '' && (
          <p className="hint">
            A day runs midnight to midnight <strong>UTC</strong>, because that is what the
            booking time is stored as. Sri Lanka is 5½ hours ahead, so this day starts at
            5:30am local and ends at 5:29am tomorrow. Times in the table are your own clock.
          </p>
        )}
      </div>

      <BookVisitCard />

      {checkingIn && (
        <CheckInCard
          appointment={checkingIn}
          staffId={session?.principal.id ?? ''}
          canSetHighCare={canSetHighCareLevel(role)}
          onCancel={() => setCheckingIn(null)}
          onCheckedIn={(admission) => {
            setCheckingIn(null);
            setAdmitted(admission);
          }}
        />
      )}

      {admitted && <CheckedInCard admission={admitted} onDismiss={() => setAdmitted(null)} />}

      <div className="card">
        <h2>
          {status ? appointmentStatusLabels[status] : 'All bookings'}
          {date ? '' : ' — every day'}
        </h2>

        <AppointmentTable
          appointments={appointments.data?.items ?? []}
          isLoading={appointments.isLoading}
          isError={appointments.isError}
          onRetry={() => void appointments.refetch()}
          onCheckIn={(appointment) => {
            setAdmitted(null);
            setCheckingIn(appointment);
          }}
          showDate={date === ''}
        />

        {appointments.data && appointments.data.total_items > 0 && (
          <div className="row" style={{ marginTop: '0.9rem', alignItems: 'center' }}>
            <p className="muted" style={{ flex: '2 1 14rem' }}>
              {appointments.data.total_items} booking
              {appointments.data.total_items === 1 ? '' : 's'}, page {appointments.data.page} of{' '}
              {appointments.data.total_pages}
            </p>
            <button
              type="button"
              className="secondary"
              style={{ flex: '0 0 auto' }}
              disabled={page <= 1}
              onClick={() => setPage((current) => current - 1)}
            >
              Previous
            </button>
            <button
              type="button"
              className="secondary"
              style={{ flex: '0 0 auto' }}
              disabled={page >= appointments.data.total_pages}
              onClick={() => setPage((current) => current + 1)}
            >
              Next
            </button>
          </div>
        )}
      </div>
    </>
  );
}

// ---------------------------------------------------------------------------
// The worklist
// ---------------------------------------------------------------------------

function AppointmentTable({
  appointments,
  isLoading,
  isError,
  onRetry,
  onCheckIn,
  showDate,
}: {
  appointments: Appointment[];
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  onCheckIn: (appointment: Appointment) => void;
  showDate: boolean;
}) {
  if (isLoading) {
    return <p className="empty">Loading…</p>;
  }

  // The toast already fired. "Try again" rather than an empty table, which would read as a
  // quiet day at the desk when it is really a broken request.
  if (isError) {
    return (
      <div className="empty">
        <p>Could not load the bookings.</p>
        <button type="button" className="secondary" onClick={onRetry}>
          Try again
        </button>
      </div>
    );
  }

  if (appointments.length === 0) {
    return <p className="empty">Nobody is booked in under this filter.</p>;
  }

  return (
    <table>
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
          <tr key={appointment.id}>
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
            <td>{appointment.reason ?? <span className="muted">Not given</span>}</td>
            <td>
              {/* A raw staff id tells nobody anything. Which of the two paths it came down does. */}
              {appointment.booked_by_staff_id ? 'At the desk' : 'In the app'}
            </td>
            <td>
              <span
                className={appointment.status === 'scheduled' ? 'badge' : 'badge retired'}
              >
                {appointmentStatusLabels[appointment.status]}
              </span>
            </td>
            <td>
              {/* Only a scheduled booking can be checked in. Every other status is finished
                  with, and offering a button that always 409s is worse than no button. */}
              {appointment.status === 'scheduled' && (
                <button type="button" onClick={() => onCheckIn(appointment)}>
                  Check in
                </button>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

// ---------------------------------------------------------------------------
// Book a visit
// ---------------------------------------------------------------------------

function BookVisitCard() {
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

      // Every listAppointments query, not just the filter on screen: the new booking may well
      // be for a day the user is not looking at, and a stale "today" is the one they will
      // come back to.
      queryClient.invalidateQueries({
        predicate: (query) =>
          (query.queryKey[0] as { _id?: string } | undefined)?._id === 'listAppointments',
      });

      setPatient(null);
      setSearch('');
      setSubmitted('');
      setWhen('');
      setReason('');
    },
  });

  // The server refuses a time in the past with cl_pat_009, and it is always a typo. Catching
  // it here means the desk sees it before the round trip, not as a red toast afterwards.
  const isFuture = when !== '' && new Date(when).getTime() > Date.now();

  function submit(event: FormEvent) {
    event.preventDefault();
    if (!patient || !isFuture) return;

    book.mutate({
      body: {
        patient_id: patient.id,
        // The input is local wall-clock; the wire is UTC. Date does the conversion, and this
        // is the one place in the app where those two are allowed to differ silently.
        scheduled_at: new Date(when).toISOString(),
        reason: reason.trim().length > 0 ? reason.trim() : null,
      },
    });
  }

  return (
    <div className="card">
      <h2>Book a visit</h2>
      <p className="muted" style={{ marginBottom: '0.9rem' }}>
        For someone on the phone or at the counter. A patient booking in the app takes the same
        slot a different way — one open booking each, and never for someone already admitted.
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
              Nobody matches “{submitted}”. A patient has to be registered before a visit can be
              booked for them — register them on the intake screen first.
            </p>
          )}

          {!patients.isFetching && (patients.data?.items.length ?? 0) > 0 && (
            <table>
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
                        Book for them
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
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
              <label htmlFor="book-when">When are they coming?</label>
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
                  That time has already passed. Somebody who is here now is admitted, not
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

// ---------------------------------------------------------------------------
// Check in
// ---------------------------------------------------------------------------

function CheckInCard({
  appointment,
  staffId,
  canSetHighCare,
  onCancel,
  onCheckedIn,
}: {
  appointment: Appointment;
  staffId: string;
  canSetHighCare: boolean;
  onCancel: () => void;
  onCheckedIn: (admission: Admission) => void;
}) {
  const queryClient = useQueryClient();

  // A nurse is offered three levels, the duty manager five. Hidden rather than disabled: the
  // server refuses icu and hdu from a nurse with cl_pat_011, and a picker that offers a choice
  // it knows will be refused is just a slower way of saying no.
  const levels = canSetHighCare ? dutyManagerCareLevels : deskCareLevels;

  const [category, setCategory] = useState<AdmissionCategory>('outpatient');
  const [urgency, setUrgency] = useState<AdmissionUrgency>('routine');
  const [isInfectious, setIsInfectious] = useState(false);

  const checkIn = useMutation({
    ...checkInAppointmentMutation(),
    onSuccess: (admission) => {
      toast.success(`${appointment.patient.full_name} checked in.`);

      // Two lists change: the booking is no longer expected, and there is a new admission on
      // the worklist. A mutation invalidates everything it changed, not just what is on screen.
      queryClient.invalidateQueries({
        predicate: (query) => {
          const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;
          return id === 'listAppointments' || id === 'listAdmissions';
        },
      });

      onCheckedIn(admission);
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    checkIn.mutate({
      path: { id: appointment.id },
      body: {
        admission_category: category,
        // Recorded proof a human chose the care level. It is you, because you are the one
        // filling this in — no code path lets an agent supply it.
        category_set_by_staff_id: staffId,
        urgency,
        is_infectious: isInfectious,
      },
    });
  }

  return (
    <div className="card">
      <h2>Check in {appointment.patient.full_name}</h2>
      <p className="muted" style={{ marginBottom: '0.9rem' }}>
        Booked for {localDateTime(appointment.scheduled_at)}
        {appointment.reason ? ` — ${appointment.reason}` : ''}. From here they are an ordinary
        admission and the bed search runs on them exactly as it would for a walk-in.
      </p>

      <form onSubmit={submit}>
        <div className="row">
          <div className="field">
            <label htmlFor="checkin-category">Care level</label>
            <select
              id="checkin-category"
              value={category}
              onChange={(event) => setCategory(event.target.value as AdmissionCategory)}
            >
              {levels.map((value) => (
                <option key={value} value={value}>
                  {admissionCategoryLabels[value]}
                </option>
              ))}
            </select>
            <p className="hint">
              {admissionCategoryHints[category]} You are choosing this and it is recorded
              against your name.
            </p>
            {!canSetHighCare && (
              <p className="hint">
                Intensive care and high dependency are not on this list because they are the
                duty manager&rsquo;s call. If this patient needs either, ask them to check the
                patient in.
              </p>
            )}
          </div>
          <div className="field">
            <label htmlFor="checkin-urgency">Urgency</label>
            <select
              id="checkin-urgency"
              value={urgency}
              onChange={(event) => setUrgency(event.target.value as AdmissionUrgency)}
            >
              {admissionUrgencies.map((value) => (
                <option key={value} value={value}>
                  {admissionUrgencyLabels[value]}
                </option>
              ))}
            </select>
          </div>
        </div>

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
          <p className="hint">Forces an isolation-capable bed when the bed agent runs.</p>
        </div>

        <div className="row">
          <button type="submit" disabled={checkIn.isPending}>
            {checkIn.isPending ? 'Checking in…' : 'Check in and admit'}
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
      <h2>Checked in</h2>
      <p className="muted">
        {admission.patient?.full_name ?? 'The patient'} is admitted and waiting for a bed. Care
        level: <strong>{admissionCategoryLabels[admission.admission_category]}</strong>.
      </p>

      {admission.missing_fields.length > 0 && (
        <>
          <p style={{ marginTop: '0.9rem' }}>
            <strong>Paperwork still outstanding:</strong>
          </p>
          <ul>
            {admission.missing_fields.map((field) => (
              <li key={field}>{detailFieldLabel(field)}</li>
            ))}
          </ul>
          <p className="muted">
            Missing paperwork does not block the admission. It is a list to chase, not a gate.
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
