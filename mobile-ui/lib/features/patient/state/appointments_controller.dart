import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/appointment_status.dart';
import '../../../services/api_client/models/my_appointment.dart';
import '../services/patient_service.dart';

class AppointmentsController extends ChangeNotifier {
  AppointmentsController(this._service);

  final PatientService _service;

  AsyncData<List<MyAppointment>> _appointments = const AsyncData.loading();
  bool _busy = false;

  AsyncData<List<MyAppointment>> get appointments => _appointments;

  /// True while a booking or cancellation is in flight, so the screen can stop
  /// a second tap turning into a second appointment.
  bool get busy => _busy;

  List<MyAppointment> get upcoming =>
      (_appointments.valueOrNull ?? const []).where(_isOpen).toList()
        ..sort((a, b) => a.scheduledAt.compareTo(b.scheduledAt));

  List<MyAppointment> get past =>
      (_appointments.valueOrNull ?? const []).where((a) => !_isOpen(a)).toList()
        ..sort((a, b) => b.scheduledAt.compareTo(a.scheduledAt));

  static bool _isOpen(MyAppointment appointment) =>
      appointment.status == AppointmentStatus.scheduled ||
      appointment.status == AppointmentStatus.checkedIn;

  Future<void> load() async {
    _appointments = const AsyncData.loading();
    notifyListeners();

    try {
      final page = await _service.loadMyAppointments();
      _appointments = AsyncData.ready(page.items);
    } on ApiException catch (error) {
      _appointments = AsyncData.failed(error);
    }
    notifyListeners();
  }

  /// Returns the error rather than swallowing it: booking fails for reasons the
  /// patient can act on — one open booking at a time, and none while admitted.
  Future<ApiException?> book({required DateTime scheduledAt, String? reason}) async {
    return _write(() => _service.bookAppointment(scheduledAt: scheduledAt, reason: reason));
  }

  Future<ApiException?> cancel(String appointmentId) async {
    return _write(() => _service.cancelAppointment(appointmentId));
  }

  Future<ApiException?> _write(Future<MyAppointment> Function() request) async {
    _busy = true;
    notifyListeners();

    try {
      await request();
      await load();
      return null;
    } on ApiException catch (error) {
      return error;
    } finally {
      _busy = false;
      notifyListeners();
    }
  }
}
