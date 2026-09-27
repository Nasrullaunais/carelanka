import 'package:dio/dio.dart';

import '../../../core/network/api.dart';
import '../models/staff_leave_item.dart';

abstract interface class StaffLeaveService {
  Future<List<StaffLeaveItem>> getMyLeaveRequests({String? status});
  Future<StaffLeaveItem> createLeaveRequest({
    required String type,
    required String startDate,
    required String endDate,
    String? reason,
    String? swapShiftId,
    String? swapWithStaffMemberId,
  });
  Future<void> withdrawLeaveRequest(String id);
}

final class DioStaffLeaveService implements StaffLeaveService {
  const DioStaffLeaveService(this._dio);

  final Dio _dio;

  @override
  Future<List<StaffLeaveItem>> getMyLeaveRequests({String? status}) async {
    final queryParams = <String, dynamic>{};
    if (status != null && status.isNotEmpty) {
      queryParams['status'] = status;
    }

    final response = await callApi(() => _dio.get<List<dynamic>>(
          '/api/me/leave-requests',
          queryParameters: queryParams.isEmpty ? null : queryParams,
        ));

    final list = response.data ?? [];
    return list
        .map((e) => StaffLeaveItem.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<StaffLeaveItem> createLeaveRequest({
    required String type,
    required String startDate,
    required String endDate,
    String? reason,
    String? swapShiftId,
    String? swapWithStaffMemberId,
  }) async {
    final payload = <String, dynamic>{
      'type': type,
      'start_date': startDate,
      'end_date': endDate,
      if (reason != null && reason.trim().isNotEmpty) 'reason': reason.trim(),
      if (swapShiftId != null && swapShiftId.isNotEmpty) 'swap_shift_id': swapShiftId,
      if (swapWithStaffMemberId != null && swapWithStaffMemberId.isNotEmpty)
        'swap_with_staff_member_id': swapWithStaffMemberId,
    };

    final response = await callApi(() => _dio.post<Map<String, dynamic>>(
          '/api/me/leave-requests',
          data: payload,
        ));

    return StaffLeaveItem.fromJson(response.data!);
  }

  @override
  Future<void> withdrawLeaveRequest(String id) async {
    await callApi(() => _dio.delete<void>(
          '/api/me/leave-requests/$id',
        ));
  }
}
