import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/my_lab_report.dart';
import '../services/patient_service.dart';

class LabReportsController extends ChangeNotifier {
  LabReportsController(this._service);

  final PatientService _service;

  AsyncData<List<MyLabReport>> _reports = const AsyncData.loading();

  AsyncData<List<MyLabReport>> get reports => _reports;

  Future<void> load() async {
    _reports = const AsyncData.loading();
    notifyListeners();

    try {
      final page = await _service.loadMyLabReports();
      _reports = AsyncData.ready(page.items);
    } on ApiException catch (error) {
      _reports = AsyncData.failed(error);
    }
    notifyListeners();
  }
}
