// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/decide_leave_request.dart';
import '../models/decide_leave_response.dart';
import '../models/leave_request_detail_dto.dart';
import '../models/leave_request_dto_paged_result.dart';
import '../models/leave_status.dart';
import '../models/leave_type.dart';

part 'leave_api.g.dart';

@RestApi()
abstract class LeaveApi {
  factory LeaveApi(Dio dio, {String? baseUrl}) = _LeaveApi;

  @GET('/leave-requests')
  Future<LeaveRequestDtoPagedResult> listLeaveRequests({
    @Query('staffMemberId') String? staffMemberId,
    @Query('status') LeaveStatus? status,
    @Query('type') LeaveType? type,
    @Query('from') DateTime? from,
    @Query('to') DateTime? to,
    @Query('page') int? page,
    @Query('pageSize') int? pageSize,
  });

  @GET('/leave-requests/{id}')
  Future<LeaveRequestDetailDto> getLeaveRequest({
    @Path('id') required String id,
  });

  @POST('/leave-requests/{id}/decision')
  Future<DecideLeaveResponse> decideLeaveRequest({
    @Path('id') required String id,
    @Body() DecideLeaveRequest? body,
  });
}
