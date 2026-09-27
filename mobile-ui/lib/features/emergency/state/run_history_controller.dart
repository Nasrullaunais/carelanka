import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/dispatch_summary.dart';
import '../services/crew_run_service.dart';

class RunHistory {
  const RunHistory(this.items, {required this.hasMore});

  final List<DispatchSummary> items;
  final bool hasMore;
}

class RunHistoryController extends ChangeNotifier {
  RunHistoryController(this._service);

  final CrewRunService _service;

  AsyncData<RunHistory> _state = const AsyncData.loading();
  bool _loadingMore = false;
  ApiException? _moreError;
  int _page = 0;

  AsyncData<RunHistory> get state => _state;
  bool get loadingMore => _loadingMore;
  ApiException? get moreError => _moreError;

  Future<void> load() async {
    _state = const AsyncData.loading();
    _page = 0;
    _moreError = null;
    notifyListeners();
    try {
      final result = await _service.history(page: 1);
      _page = 1;
      _state = AsyncData.ready(
        RunHistory(result.items, hasMore: result.page < result.totalPages),
      );
    } on ApiException catch (error) {
      _state = AsyncData.failed(error);
    }
    notifyListeners();
  }

  Future<void> loadMore() async {
    final current = _state.valueOrNull;
    if (current == null || !current.hasMore || _loadingMore) return;
    _loadingMore = true;
    _moreError = null;
    notifyListeners();
    try {
      final result = await _service.history(page: _page + 1);
      _page = result.page;
      _state = AsyncData.ready(
        RunHistory([
          ...current.items,
          ...result.items,
        ], hasMore: result.page < result.totalPages),
      );
    } on ApiException catch (error) {
      _moreError = error;
    }
    _loadingMore = false;
    notifyListeners();
  }
}
