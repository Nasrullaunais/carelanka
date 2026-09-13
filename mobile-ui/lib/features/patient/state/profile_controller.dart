import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/gender.dart';
import '../../../services/api_client/models/my_profile.dart';
import '../../../services/api_client/models/pre_register_request.dart';
import '../services/patient_service.dart';

/// Whether this login has a hospital record behind it, and what it says.
///
/// Nothing else under `/me/*` works until it does, so this is loaded once when
/// the patient area opens and shared by every tab.
class ProfileController extends ChangeNotifier {
  ProfileController(this._service);

  final PatientService _service;

  AsyncData<MyProfile?> _profile = const AsyncData.loading();
  bool _saving = false;
  ApiException? _saveError;

  /// `null` inside [AsyncReady] means the account exists but has no record yet.
  AsyncData<MyProfile?> get profile => _profile;
  bool get saving => _saving;
  ApiException? get saveError => _saveError;
  Map<String, List<String>> get fieldErrors => _saveError?.fieldErrors ?? const {};

  bool get isLinked => _profile.valueOrNull != null;

  Future<void> load() async {
    _profile = const AsyncData.loading();
    notifyListeners();

    try {
      _profile = AsyncData.ready(await _service.loadMyProfile());
    } on ApiException catch (error) {
      _profile = error.code == PatientService.notLinkedCode
          ? const AsyncData.ready(null)
          : AsyncData.failed(error);
    }
    notifyListeners();
  }

  /// Creates the record on first save, and updates it afterwards — the endpoint
  /// matches on NIC, so calling it twice lands on the same record.
  Future<bool> save({
    required String nic,
    required String fullName,
    required Gender gender,
    DateTime? dateOfBirth,
    String? phone,
    String? address,
    String? emergencyContactName,
    String? emergencyContactPhone,
  }) async {
    _saving = true;
    _saveError = null;
    notifyListeners();

    try {
      final saved = await _service.saveMyDetails(PreRegisterRequest(
        nic: nic,
        fullName: fullName,
        gender: gender,
        dateOfBirth: dateOfBirth,
        phone: _blankToNull(phone),
        address: _blankToNull(address),
        emergencyContactName: _blankToNull(emergencyContactName),
        emergencyContactPhone: _blankToNull(emergencyContactPhone),
      ));
      _profile = AsyncData.ready(saved);
      return true;
    } on ApiException catch (error) {
      _saveError = error;
      return false;
    } finally {
      _saving = false;
      notifyListeners();
    }
  }

  static String? _blankToNull(String? value) {
    final trimmed = value?.trim();
    return (trimmed == null || trimmed.isEmpty) ? null : trimmed;
  }
}
