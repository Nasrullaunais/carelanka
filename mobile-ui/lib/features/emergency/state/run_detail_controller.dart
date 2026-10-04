import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/dispatch_detail.dart';
import '../services/crew_run_service.dart';

class RunDetailController extends ChangeNotifier {
  RunDetailController(this._service, this.runId);

  final CrewRunService _service;
  final String runId;

  AsyncData<DispatchDetail> _state = const AsyncData.loading();

  AsyncData<DispatchDetail> get state => _state;

  Future<void> load() async {
    _state = const AsyncData.loading();
    notifyListeners();
    try {
      _state = AsyncData.ready(await _service.run(runId));
    } on ApiException catch (error) {
      _state = AsyncData.failed(error);
    }
    notifyListeners();
  }
}
