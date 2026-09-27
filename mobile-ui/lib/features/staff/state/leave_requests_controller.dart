import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../models/staff_leave_item.dart';
import '../services/staff_leave_service.dart';

class LeaveRequestsController extends ChangeNotifier {
  LeaveRequestsController(this._service);

  final StaffLeaveService _service;

  AsyncData<List<StaffLeaveItem>> _state = const AsyncData.loading();
  bool _busy = false;
  ApiException? _actionError;
  String? _actionRequestId;
  String? _currentStatusFilter;

  AsyncData<List<StaffLeaveItem>> get state => _state;
  bool get busy => _busy;
  ApiException? get actionError => _actionError;
  String? get actionRequestId => _actionRequestId;
  String? get currentStatusFilter => _currentStatusFilter;

  Future<void> load({bool showLoading = true, String? statusFilter}) async {
    _currentStatusFilter = statusFilter;
    if (showLoading) {
      _state = const AsyncData.loading();
      _actionError = null;
      notifyListeners();
    }

    try {
      final requests = await _service.getMyLeaveRequests(status: statusFilter);
      _state = AsyncData.ready(requests);
    } on ApiException catch (error) {
      if (showLoading || _state is! AsyncReady<List<StaffLeaveItem>>) {
        _state = AsyncData.failed(error);
      } else {
        _actionError = error;
      }
    }

    notifyListeners();
  }

  Future<bool> submitRequest({
    required String type,
    required String startDate,
    required String endDate,
    String? reason,
    String? swapShiftId,
    String? swapWithStaffMemberId,
  }) async {
    if (_busy) return false;

    _busy = true;
    _actionError = null;
    notifyListeners();

    try {
      await _service.createLeaveRequest(
        type: type,
        startDate: startDate,
        endDate: endDate,
        reason: reason,
        swapShiftId: swapShiftId,
        swapWithStaffMemberId: swapWithStaffMemberId,
      );
      await load(showLoading: false, statusFilter: _currentStatusFilter);
      _busy = false;
      notifyListeners();
      return true;
    } on ApiException catch (error) {
      _actionError = error;
      _busy = false;
      notifyListeners();
      return false;
    }
  }

  Future<bool> withdrawRequest(String id) async {
    if (_busy) return false;

    _busy = true;
    _actionRequestId = id;
    _actionError = null;
    notifyListeners();

    try {
      await _service.withdrawLeaveRequest(id);
      await load(showLoading: false, statusFilter: _currentStatusFilter);
      _busy = false;
      _actionRequestId = null;
      notifyListeners();
      return true;
    } on ApiException catch (error) {
      _actionError = error;
      _busy = false;
      _actionRequestId = null;
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
