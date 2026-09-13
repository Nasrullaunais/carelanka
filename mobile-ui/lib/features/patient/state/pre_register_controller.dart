import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../services/api_client/models/gender.dart';
import '../../../services/api_client/models/pre_register_request.dart';
import '../services/patient_service.dart';

/// Creates the hospital record behind a patient login. Creates no admission —
/// booking a visit is a separate step.
class PreRegisterController extends ChangeNotifier {
  PreRegisterController(this._service);

  final PatientService _service;

  bool _submitting = false;
  ApiException? _error;

  bool get submitting => _submitting;
  ApiException? get error => _error;

  /// Server-side validation messages, keyed by field name, so the form can show
  /// them against the field that caused them.
  Map<String, List<String>> get fieldErrors => _error?.fieldErrors ?? const {};

  Future<bool> submit({
    required String nic,
    required String fullName,
    required Gender gender,
    DateTime? dateOfBirth,
    String? phone,
    String? address,
    String? emergencyContactName,
    String? emergencyContactPhone,
  }) async {
    _submitting = true;
    _error = null;
    notifyListeners();

    try {
      await _service.preRegister(PreRegisterRequest(
        nic: nic,
        fullName: fullName,
        gender: gender,
        dateOfBirth: dateOfBirth,
        phone: _blankToNull(phone),
        address: _blankToNull(address),
        emergencyContactName: _blankToNull(emergencyContactName),
        emergencyContactPhone: _blankToNull(emergencyContactPhone),
      ));
      return true;
    } on ApiException catch (error) {
      _error = error;
      return false;
    } finally {
      _submitting = false;
      notifyListeners();
    }
  }

  static String? _blankToNull(String? value) {
    final trimmed = value?.trim();
    return (trimmed == null || trimmed.isEmpty) ? null : trimmed;
  }
}
