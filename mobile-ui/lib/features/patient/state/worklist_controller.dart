import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/worklist_row.dart';
import '../services/patient_service.dart';

/// The ward nurse's board: every booking and visit for today, in one list.
class WorklistController extends ChangeNotifier {
  WorklistController(this._service);

  final PatientService _service;

  AsyncData<List<WorklistRow>> _rows = const AsyncData.loading();
  String _search = '';
  bool _includeFinished = false;

  AsyncData<List<WorklistRow>> get rows => _rows;
  String get search => _search;
  bool get includeFinished => _includeFinished;

  Future<void> load() async {
    _rows = const AsyncData.loading();
    notifyListeners();

    try {
      final page = await _service.loadWorklist(
        search: _search,
        includeFinished: _includeFinished,
      );
      _rows = AsyncData.ready(page.items);
    } on ApiException catch (error) {
      _rows = AsyncData.failed(error);
    }
    notifyListeners();
  }

  Future<void> setSearch(String value) {
    _search = value.trim();
    return load();
  }

  Future<void> setIncludeFinished(bool value) {
    _includeFinished = value;
    return load();
  }
}
