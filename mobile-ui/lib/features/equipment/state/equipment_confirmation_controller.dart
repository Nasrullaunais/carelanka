import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/equipment_item.dart';
import '../services/equipment_confirmation_service.dart';

class EquipmentConfirmationController extends ChangeNotifier {
  EquipmentConfirmationController(this._service);

  final EquipmentConfirmationService _service;

  AsyncData<int> _awaitingCount = const AsyncData.loading();

  // Kept only in memory for this screen's lifetime. Every list, confirm and reject call sends it,
  // and the API is what decides whether it is right.
  String? _code;
  bool _unlocking = false;
  String? _unlockProblem;

  AsyncData<List<EquipmentItem>> _items = const AsyncData.loading();
  final Set<String> _busy = {};

  AsyncData<int> get awaitingCount => _awaitingCount;
  bool get isUnlocked => _code != null;
  bool get unlocking => _unlocking;
  String? get unlockProblem => _unlockProblem;
  AsyncData<List<EquipmentItem>> get items => _items;

  bool isBusy(String id) => _busy.contains(id);

  Future<void> loadCount() async {
    _awaitingCount = const AsyncData.loading();
    notifyListeners();

    try {
      _awaitingCount = AsyncData.ready(await _service.countAwaiting());
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
      final items = await _service.listAwaiting(entered);
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
      final items = await _service.listAwaiting(code);
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
  Future<ApiException?> confirm(EquipmentItem item) =>
      _act(item, (code) => _service.confirm(item.id, code));

  /// Returns the server's refusal, or null when the item was rejected.
  Future<ApiException?> reject(EquipmentItem item) =>
      _act(item, (code) => _service.reject(item.id, code));

  Future<ApiException?> _act(EquipmentItem item, Future<Object?> Function(String code) call) async {
    final code = _code;
    if (code == null || _busy.contains(item.id)) return null;

    _busy.add(item.id);
    notifyListeners();

    try {
      await call(code);
      _remove(item.id);
      return null;
    } on ApiException catch (error) {
      // 404 or 409 means another administrator already dealt with it, so it no longer belongs here.
      if (error.isNotFound || error.isConflict) _remove(item.id);
      if (error.isForbidden) lock(problem: error.message);
      return error;
    } finally {
      _busy.remove(item.id);
      notifyListeners();
    }
  }

  void _remove(String id) {
    final current = _items.valueOrNull;
    if (current == null) return;

    final remaining = current.where((item) => item.id != id).toList();
    _items = AsyncData.ready(remaining);
    _awaitingCount = AsyncData.ready(remaining.length);
  }
}
