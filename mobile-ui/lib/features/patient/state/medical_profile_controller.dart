import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/patient_medical_profile.dart';
import '../../../services/api_client/models/update_medical_profile_request.dart';
import '../services/patient_service.dart';

class MedicalProfileController extends ChangeNotifier {
  MedicalProfileController(this._service, this.patientId);

  final PatientService _service;
  final String patientId;

  AsyncData<PatientMedicalProfile> _profile = const AsyncData.loading();
  bool _saving = false;
  ApiException? _saveError;

  AsyncData<PatientMedicalProfile> get profile => _profile;
  bool get saving => _saving;
  ApiException? get saveError => _saveError;

  Future<void> load() async {
    _profile = const AsyncData.loading();
    notifyListeners();

    try {
      _profile = AsyncData.ready(await _service.loadMedicalProfile(patientId));
    } on ApiException catch (error) {
      _profile = AsyncData.failed(error);
    }
    notifyListeners();
  }

  /// Returns true when the save landed, so the screen can close itself only on success.
  Future<bool> save(UpdateMedicalProfileRequest request) async {
    _saving = true;
    _saveError = null;
    notifyListeners();

    try {
      _profile = AsyncData.ready(await _service.saveMedicalProfile(patientId, request));
      return true;
    } on ApiException catch (error) {
      _saveError = error;
      return false;
    } finally {
      _saving = false;
      notifyListeners();
    }
  }
}
