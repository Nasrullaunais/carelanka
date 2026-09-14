import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/features/patient/state/appointments_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/appointment_status.dart';
import 'package:carelanka_mobile/services/api_client/models/my_appointment.dart';
import 'package:flutter_test/flutter_test.dart';

import 'fake_patient_service.dart';

MyAppointment _appointment({
  required String id,
  required AppointmentStatus status,
  required DateTime at,
}) {
  return MyAppointment(
    appointmentId: id,
    scheduledAt: at,
    status: status,
    statusText: status.json ?? 'unknown',
    canCancel: status == AppointmentStatus.scheduled,
  );
}

void main() {
  final now = DateTime.utc(2026, 9, 14, 10);

  test('open bookings and finished ones are separated, each in its own order', () async {
    final service = FakePatientService()
      ..appointmentsResult = appointmentPage([
        _appointment(
            id: 'later',
            status: AppointmentStatus.scheduled,
            at: now.add(const Duration(days: 5))),
        _appointment(
            id: 'done',
            status: AppointmentStatus.completed,
            at: now.subtract(const Duration(days: 2))),
        _appointment(
            id: 'soon',
            status: AppointmentStatus.scheduled,
            at: now.add(const Duration(days: 1))),
        _appointment(
            id: 'older',
            status: AppointmentStatus.cancelled,
            at: now.subtract(const Duration(days: 9))),
      ]);
    final controller = AppointmentsController(service);

    await controller.load();

    expect(controller.upcoming.map((a) => a.appointmentId), ['soon', 'later']);
    expect(controller.past.map((a) => a.appointmentId), ['done', 'older']);
  });

  test('a checked-in visit still counts as open', () async {
    final service = FakePatientService()
      ..appointmentsResult = appointmentPage([
        _appointment(id: 'here', status: AppointmentStatus.checkedIn, at: now),
      ]);
    final controller = AppointmentsController(service);

    await controller.load();

    expect(controller.upcoming.single.appointmentId, 'here');
    expect(controller.past, isEmpty);
  });

  test('a refused booking comes back to the screen instead of being swallowed', () async {
    final service = FakePatientService()
      ..appointmentsResult = appointmentPage([])
      ..bookResult = const ApiException(
        message: 'Test Patient already has a visit booked.',
        statusCode: 409,
        code: 'cl_pat_009',
      );
    final controller = AppointmentsController(service);

    final error = await controller.book(scheduledAt: now.add(const Duration(days: 3)));

    expect(error?.code, 'cl_pat_009');
    expect(controller.busy, isFalse);
  });

  test('a successful booking reports no error and reloads the list', () async {
    final service = FakePatientService()
      ..appointmentsResult = appointmentPage([])
      ..bookResult = _appointment(id: 'new', status: AppointmentStatus.scheduled, at: now);
    final controller = AppointmentsController(service);

    final error = await controller.book(scheduledAt: now.add(const Duration(days: 3)));

    expect(error, isNull);
    expect(service.bookCalls, 1);
  });
}
