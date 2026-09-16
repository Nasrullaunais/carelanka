import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/gender.dart';
import '../../../services/api_client/models/my_profile.dart';
import '../../../services/api_client/models/patient_claim_preview.dart';
import '../../../services/api_client/models/pre_register_request.dart';
import '../services/patient_service.dart';

class ProfileController extends ChangeNotifier {
  ProfileController(this._service);

  final PatientService _service;

  AsyncData<MyProfile?> _profile = const AsyncData.loading();
  bool _saving = false;
  ApiException? _saveError;

  // `null` inside AsyncReady means the account exists but has no hospital record yet.
  AsyncData<MyProfile?> get profile => _profile;
  bool get saving => _saving;
  ApiException? get saveError => _saveError;
  Map<String, List<String>> get fieldErrors => _saveError?.fieldErrors ?? const {};

  bool get isLinked => _profile.valueOrNull != null;

  Future<void> load({bool showLoading = true}) async {
    if (showLoading) {
      _profile = const AsyncData.loading();
      notifyListeners();
    }

    try {
      _profile = AsyncData.ready(await _service.loadMyProfile());
    } on ApiException catch (error) {
      _profile = error.code == PatientService.notLinkedCode
          ? const AsyncData.ready(null)
          : AsyncData.failed(error);
    }
    notifyListeners();
  }

  // The endpoint matches on NIC — creates on first save, updates on later ones, so calling it twice is safe.
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

  /// Looks up the record a patient code belongs to, masked, so they can confirm it is
  /// theirs before [claim] attaches their login to it. Writes nothing.
  Future<PatientClaimPreview?> previewClaim({
    required String patientCode,
    required String nic,
  }) async {
    _saving = true;
    _saveError = null;
    notifyListeners();

    try {
      return await _service.previewClaim(
        patientCode: patientCode,
        nic: nic,
      );
    } on ApiException catch (error) {
      _saveError = error;
      return null;
    } finally {
      _saving = false;
      notifyListeners();
    }
  }

  Future<bool> claim({
    required String patientCode,
    required String nic,
  }) async {
    _saving = true;
    _saveError = null;
    notifyListeners();

    try {
      _profile = AsyncData.ready(
        await _service.claimRecord(patientCode: patientCode, nic: nic),
      );
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
