// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/create_staff_member_request.dart';
import '../models/crew_candidate.dart';
import '../models/deactivate_staff_member_request.dart';
import '../models/staff_member_detail_dto.dart';
import '../models/staff_member_dto.dart';
import '../models/staff_role.dart';
import '../models/staff_summary_dto_paged_result.dart';
import '../models/update_staff_member_request.dart';
import '../models/update_staff_member_response.dart';

part 'staff_api.g.dart';

@RestApi()
abstract class StaffApi {
  factory StaffApi(Dio dio, {String? baseUrl}) = _StaffApi;

  @POST('/staff')
  Future<StaffMemberDto> createStaffMember({
    @Body() CreateStaffMemberRequest? body,
  });

  @GET('/staff')
  Future<StaffSummaryDtoPagedResult> listStaff({
    @Query('search') String? search,
    @Query('role') StaffRole? role,
    @Query('skill') String? skill,
    @Query('department') String? department,
    @Query('includeInactive') bool? includeInactive,
    @Query('page') int? page,
    @Query('pageSize') int? pageSize,
    @Query('sortBy') String? sortBy,
    @Query('sortDir') String? sortDir,
  });

  @GET('/staff/departments')
  Future<List<String>> listStaffDepartments();

  @GET('/staff/{id}')
  Future<StaffMemberDetailDto> getStaffMember({
    @Path('id') required String id,
  });

  @PUT('/staff/{id}')
  Future<UpdateStaffMemberResponse> updateStaffMember({
    @Path('id') required String id,
    @Body() UpdateStaffMemberRequest? body,
  });

  @POST('/staff/{id}/deactivate')
  Future<void> deactivateStaffMember({
    @Path('id') required String id,
    @Body() DeactivateStaffMemberRequest? body,
  });

  @POST('/staff/{id}/reactivate')
  Future<StaffMemberDto> reactivateStaffMember({
    @Path('id') required String id,
  });

  @GET('/staff/crew-candidates')
  Future<List<CrewCandidate>> searchAvailableCrew({
    @Query('search') String? search,
  });
}
