import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../models/my_shift_item.dart';
import '../services/staff_roster_service.dart';

class MyShiftsController extends ChangeNotifier {
  MyShiftsController(this._service);

  final StaffRosterService _service;

  AsyncData<List<MyShiftItem>> _state = const AsyncData.loading();
  bool _busy = false;
  ApiException? _actionError;
  String? _actionAllocationId;

  AsyncData<List<MyShiftItem>> get state => _state;
  bool get busy => _busy;
  ApiException? get actionError => _actionError;
  String? get actionAllocationId => _actionAllocationId;

  Future<void> load({bool showLoading = true}) async {
    if (showLoading) {
      _state = const AsyncData.loading();
      _actionError = null;
      notifyListeners();
    }

    try {
      final shifts = await _service.getMyShifts();
      _state = AsyncData.ready(shifts);
    } on ApiException catch (error) {
      if (showLoading || _state is! AsyncReady<List<MyShiftItem>>) {
        _state = AsyncData.failed(error);
      } else {
        _actionError = error;
      }
    }

    notifyListeners();
  }

  Future<bool> clockIn(String allocationId) async {
    if (_busy) return false;

    _busy = true;
    _actionAllocationId = allocationId;
    _actionError = null;
    notifyListeners();

    try {
      await _service.clockIn(allocationId);
      await load(showLoading: false);
      _busy = false;
      _actionAllocationId = null;
      notifyListeners();
      return true;
    } on ApiException catch (error) {
      _actionError = error;
      _busy = false;
      _actionAllocationId = null;
      notifyListeners();
      return false;
    }
  }

  Future<bool> clockOut(String allocationId) async {
    if (_busy) return false;

    _busy = true;
    _actionAllocationId = allocationId;
    _actionError = null;
    notifyListeners();

    try {
      await _service.clockOut(allocationId);
      await load(showLoading: false);
      _busy = false;
      _actionAllocationId = null;
      notifyListeners();
      return true;
    } on ApiException catch (error) {
      _actionError = error;
      _busy = false;
      _actionAllocationId = null;
      notifyListeners();
      return false;
    }
  }

  void clearError() {
    if (_actionError != null) {
      _actionError = null;
      notifyListeners();
    }
  }
}
