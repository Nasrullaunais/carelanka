// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/allocation_dto.dart';
import '../models/create_leave_request.dart';
import '../models/leave_request_detail_dto.dart';
import '../models/leave_request_dto.dart';
import '../models/leave_status.dart';
import '../models/my_shift_dto.dart';

part 'my_roster_api.g.dart';

@RestApi()
abstract class MyRosterApi {
  factory MyRosterApi(Dio dio, {String? baseUrl}) = _MyRosterApi;

  @POST('/me/allocations/{id}/clock-in')
  Future<AllocationDto> clockIn({
    @Path('id') required String id,
  });

  @POST('/me/allocations/{id}/clock-out')
  Future<AllocationDto> clockOut({
    @Path('id') required String id,
  });

  @GET('/me/leave-requests')
  Future<List<LeaveRequestDto>> getMyLeaveRequests({
    @Query('status') LeaveStatus? status,
  });

  @POST('/me/leave-requests')
  Future<LeaveRequestDetailDto> createMyLeaveRequest({
    @Body() CreateLeaveRequest? body,
  });

  @DELETE('/me/leave-requests/{id}')
  Future<void> withdrawMyLeaveRequest({
    @Path('id') required String id,
  });

  @GET('/me/shifts')
  Future<List<MyShiftDto>> getMyShifts({
    @Query('from') DateTime? from,
    @Query('to') DateTime? to,
    @Query('includePast') bool? includePast,
  });
}
