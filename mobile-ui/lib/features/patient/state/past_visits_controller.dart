import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/my_admission.dart';
import '../services/patient_service.dart';

/// Finished stays only. The open one lives on the My stay tab, so it is not
/// listed twice.
class PastVisitsController extends ChangeNotifier {
  PastVisitsController(this._service);

  final PatientService _service;

  AsyncData<List<MyAdmission>> _visits = const AsyncData.loading();

  AsyncData<List<MyAdmission>> get visits => _visits;

  Future<void> load() async {
    _visits = const AsyncData.loading();
    notifyListeners();

    try {
      final page = await _service.loadMyHistory();
      _visits = AsyncData.ready(page.items);
    } on ApiException catch (error) {
      _visits = AsyncData.failed(error);
    }
    notifyListeners();
  }
}
