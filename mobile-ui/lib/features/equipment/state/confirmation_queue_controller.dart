import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';

/// A queue the hospital administrator works through after entering the confirmation code:
/// newly registered equipment, and open maintenance jobs.
abstract class ConfirmationQueueController<T> extends ChangeNotifier {
  AsyncData<int> _awaitingCount = const AsyncData.loading();

  // Kept only in memory for this screen's lifetime. Every list, confirm and reject call sends it,
  // and the API is what decides whether it is right.
  String? _code;
  bool _unlocking = false;
  String? _unlockProblem;

  AsyncData<List<T>> _items = const AsyncData.loading();
  final Set<String> _busy = {};

  AsyncData<int> get awaitingCount => _awaitingCount;
  bool get isUnlocked => _code != null;
  bool get unlocking => _unlocking;
  String? get unlockProblem => _unlockProblem;
  AsyncData<List<T>> get items => _items;

  bool isBusy(T item) => _busy.contains(idOf(item));

  @protected
  String idOf(T item);

  @protected
  Future<int> fetchCount();

  @protected
  Future<List<T>> fetchAwaiting(String code);

  @protected
  Future<void> sendConfirm(T item, String code);

  Future<void> loadCount() async {
    _awaitingCount = const AsyncData.loading();
    notifyListeners();

    try {
      _awaitingCount = AsyncData.ready(await fetchCount());
    } on ApiException catch (error) {
      _awaitingCount = AsyncData.failed(error);
    }
    notifyListeners();
  }

  Future<bool> unlock(String code) async {
    final entered = code.trim();
    if (entered.isEmpty) {
      _unlockProblem = 'Enter the confirmation code.';
      notifyListeners();
      return false;
    }
    if (_unlocking) return false;

    _unlocking = true;
    _unlockProblem = null;
    notifyListeners();

    try {
      final items = await fetchAwaiting(entered);
      _code = entered;
      _items = AsyncData.ready(items);
      _awaitingCount = AsyncData.ready(items.length);
      return true;
    } on ApiException catch (error) {
      _unlockProblem = error.message;
      return false;
    } finally {
      _unlocking = false;
      notifyListeners();
    }
  }

  Future<void> reload() async {
    final code = _code;
    if (code == null) return;

    _items = const AsyncData.loading();
    notifyListeners();

    try {
      final items = await fetchAwaiting(code);
      _items = AsyncData.ready(items);
      _awaitingCount = AsyncData.ready(items.length);
    } on ApiException catch (error) {
      if (error.isForbidden) {
        lock(problem: error.message);
        return;
      }
      _items = AsyncData.failed(error);
    }
    notifyListeners();
  }

  void lock({String? problem}) {
    _code = null;
    _items = const AsyncData.loading();
    _busy.clear();
    _unlockProblem = problem;
    notifyListeners();
  }

  /// Returns the server's refusal, or null when the item was confirmed.
  Future<ApiException?> confirm(T item) => act(item, sendConfirm);

  /// Sends [call] with the unlocked code and takes [item] off the list once it has been dealt with.
  @protected
  Future<ApiException?> act(T item, Future<void> Function(T item, String code) call) async {
    final code = _code;
    final id = idOf(item);
    if (code == null || _busy.contains(id)) return null;

    _busy.add(id);
    notifyListeners();

    try {
      await call(item, code);
      _remove(id);
      return null;
    } on ApiException catch (error) {
      // 404 or 409 means another administrator already dealt with it, so it no longer belongs here.
      if (error.isNotFound || error.isConflict) _remove(id);
      if (error.isForbidden) lock(problem: error.message);
      return error;
    } finally {
      _busy.remove(id);
      notifyListeners();
    }
  }

  void _remove(String id) {
    final current = _items.valueOrNull;
    if (current == null) return;

    final remaining = current.where((item) => idOf(item) != id).toList();
    _items = AsyncData.ready(remaining);
    _awaitingCount = AsyncData.ready(remaining.length);
  }
}
