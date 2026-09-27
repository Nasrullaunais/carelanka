import 'dart:async';
import 'dart:math';

import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../services/api_client/models/call_status.dart';
import '../../../services/api_client/models/create_emergency_call_request.dart';
import '../../../services/api_client/models/my_call_tracking.dart';
import '../../../services/api_client/models/my_emergency_call_summary.dart';
import '../services/patient_emergency_service.dart';

class PatientEmergencyController extends ChangeNotifier {
  PatientEmergencyController(
    this._service, {
    this.pollInterval = const Duration(seconds: 10),
  });

  final PatientEmergencyService _service;
  final Duration pollInterval;
  Timer? _poll;
  bool _disposed = false;
  String? _submissionKey;

  List<MyEmergencyCallSummary> calls = const [];
  MyCallTracking? tracking;
  bool loading = false;
  bool acting = false;
  ApiException? error;

  Future<void> load() async {
    loading = true;
    error = null;
    _notify();
    try {
      calls = await _service.calls();
    } on ApiException catch (value) {
      error = value;
    } finally {
      loading = false;
      _notify();
    }
  }

  Future<String?> report({
    required bool patientIsCaller,
    required double latitude,
    required double longitude,
    required double accuracy,
    required DateTime capturedAt,
    String? details,
  }) async {
    if (acting) return null;
    acting = true;
    error = null;
    _submissionKey ??= _uuid();
    _notify();
    try {
      final call = await _service.report(
        CreateEmergencyCallRequest(
          patientIsCaller: patientIsCaller,
          latitude: latitude,
          longitude: longitude,
          locationAccuracyMetres: accuracy,
          locationCapturedAt: capturedAt,
          idempotencyKey: _submissionKey!,
          details: details?.trim().isEmpty == true ? null : details?.trim(),
        ),
      );
      _submissionKey = null;
      await load();
      return call.id;
    } on ApiException catch (value) {
      error = value;
      return null;
    } finally {
      acting = false;
      _notify();
    }
  }

  void watch(String id) {
    _poll?.cancel();
    refreshTracking(id);
    _poll = Timer.periodic(pollInterval, (_) => refreshTracking(id));
  }

  Future<void> refreshTracking(String id) async {
    try {
      tracking = await _service.track(id);
      error = null;
      if (_terminal(tracking?.callStatus)) _poll?.cancel();
    } on ApiException catch (value) {
      error = value;
    }
    _notify();
  }

  Future<bool> cancel(
    String id,
    String reason, {
    required bool dispatched,
  }) async {
    if (acting || reason.trim().isEmpty) return false;
    acting = true;
    error = null;
    _notify();
    try {
      if (dispatched) {
        await _service.requestCancellation(id, reason.trim());
      } else {
        await _service.cancel(id, reason.trim());
      }
      await refreshTracking(id);
      return true;
    } on ApiException catch (value) {
      error = value;
      return false;
    } finally {
      acting = false;
      _notify();
    }
  }

  void resumed(String id) => refreshTracking(id);

  static bool _terminal(CallStatus? status) =>
      status == CallStatus.completed || status == CallStatus.cancelled;

  static String _uuid() {
    final random = Random.secure();
    final bytes = List<int>.generate(16, (_) => random.nextInt(256));
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    final value = bytes
        .map((byte) => byte.toRadixString(16).padLeft(2, '0'))
        .join();
    return '${value.substring(0, 8)}-${value.substring(8, 12)}-${value.substring(12, 16)}-'
        '${value.substring(16, 20)}-${value.substring(20)}';
  }

  void _notify() {
    if (!_disposed) notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    _poll?.cancel();
    super.dispose();
  }
}
