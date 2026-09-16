import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/patient_summary.dart';
import '../services/lab_reports_service.dart';

class OpdPatientSearchController extends ChangeNotifier {
  OpdPatientSearchController(this._service);

  final LabReportsService _service;

  static const minSearchLength = 2;

  String _search = '';
  AsyncData<List<PatientSummary>>? _results;
  int _latestRequest = 0;

  String get search => _search;

  AsyncData<List<PatientSummary>>? get results => _results;

  Future<void> setSearch(String value) async {
    _search = value.trim();
    final request = ++_latestRequest;

    if (_search.length < minSearchLength) {
      _results = null;
      notifyListeners();
      return;
    }

    _results = const AsyncData.loading();
    notifyListeners();

    AsyncData<List<PatientSummary>> outcome;
    try {
      final page = await _service.searchPatients(_search);
      outcome = AsyncData.ready(page.items);
    } on ApiException catch (error) {
      outcome = AsyncData.failed(error);
    }

    // Typing fires a search per keystroke; an older, slower reply must not
    // overwrite the results for what is in the box now.
    if (request != _latestRequest) return;

    _results = outcome;
    notifyListeners();
  }

  Future<void> retry() => setSearch(_search);
}
