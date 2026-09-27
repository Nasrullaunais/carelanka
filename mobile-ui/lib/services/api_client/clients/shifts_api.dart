// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/bulk_shift_request.dart';
import '../models/bulk_shift_response.dart';
import '../models/coverage_status.dart';
import '../models/create_shift_request.dart';
import '../models/replace_ward_staffing_rules_response.dart';
import '../models/shift_detail_dto.dart';
import '../models/shift_dto.dart';
import '../models/shift_summary_dto_paged_result.dart';
import '../models/staff_role.dart';
import '../models/ward_staffing_rule_dto.dart';
import '../models/ward_staffing_rule_input.dart';

part 'shifts_api.g.dart';

@RestApi()
abstract class ShiftsApi {
  factory ShiftsApi(Dio dio, {String? baseUrl}) = _ShiftsApi;

  @GET('/shifts')
  Future<ShiftSummaryDtoPagedResult> listShifts({
    @Query('wardId') String? wardId,
    @Query('from') DateTime? from,
    @Query('to') DateTime? to,
    @Query('role') StaffRole? role,
    @Query('coverageStatus') CoverageStatus? coverageStatus,
    @Query('page') int? page,
    @Query('pageSize') int? pageSize,
    @Query('sortBy') String? sortBy,
    @Query('sortDir') String? sortDir,
  });

  @POST('/shifts')
  Future<ShiftDto> createShift({
    @Body() CreateShiftRequest? body,
  });

  @POST('/shifts/bulk')
  Future<BulkShiftResponse> createShiftsBulk({
    @Body() BulkShiftRequest? body,
  });

  @GET('/shifts/{id}')
  Future<ShiftDetailDto> getShift({
    @Path('id') required String id,
  });

  @PUT('/shifts/{id}')
  Future<ShiftDto> updateShift({
    @Path('id') required String id,
    @Body() CreateShiftRequest? body,
  });

  @DELETE('/shifts/{id}')
  Future<void> cancelShift({
    @Path('id') required String id,
  });

  @GET('/wards/{wardId}/staffing-rules')
  Future<List<WardStaffingRuleDto>> getWardStaffingRules({
    @Path('wardId') required String wardId,
  });

  @PUT('/wards/{wardId}/staffing-rules')
  Future<ReplaceWardStaffingRulesResponse> setWardStaffingRules({
    @Path('wardId') required String wardId,
    @Body() List<WardStaffingRuleInput>? body,
  });
}
