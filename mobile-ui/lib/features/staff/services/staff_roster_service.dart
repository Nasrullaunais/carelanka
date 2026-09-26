import 'package:dio/dio.dart';

import '../../../core/network/api.dart';
import '../models/my_shift_item.dart';

abstract interface class StaffRosterService {
  Future<List<MyShiftItem>> getMyShifts({DateTime? from, DateTime? to});
  Future<void> clockIn(String allocationId);
  Future<void> clockOut(String allocationId);
}

final class DioStaffRosterService implements StaffRosterService {
  const DioStaffRosterService(this._dio);

  final Dio _dio;

  @override
  Future<List<MyShiftItem>> getMyShifts({DateTime? from, DateTime? to}) async {
    final queryParams = <String, dynamic>{};
    if (from != null) {
      queryParams['from'] = from.toIso8601String().split('T').first;
    }
    if (to != null) {
      queryParams['to'] = to.toIso8601String().split('T').first;
    }

    final response = await callApi(() => _dio.get<List<dynamic>>(
          '/api/me/shifts',
          queryParameters: queryParams.isEmpty ? null : queryParams,
        ));

    final list = response.data ?? [];
    return list
        .map((e) => MyShiftItem.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<void> clockIn(String allocationId) async {
    await callApi(() => _dio.post<Map<String, dynamic>>(
          '/api/me/allocations/$allocationId/clock-in',
        ));
  }

  @override
  Future<void> clockOut(String allocationId) async {
    await callApi(() => _dio.post<Map<String, dynamic>>(
          '/api/me/allocations/$allocationId/clock-out',
        ));
  }
}
