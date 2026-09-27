// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/create_skill_request.dart';
import '../models/grant_staff_skill_request.dart';
import '../models/revoke_staff_skill_response.dart';
import '../models/skill_dto.dart';
import '../models/staff_skill_dto.dart';
import '../models/update_skill_request.dart';

part 'skills_api.g.dart';

@RestApi()
abstract class SkillsApi {
  factory SkillsApi(Dio dio, {String? baseUrl}) = _SkillsApi;

  @GET('/skills')
  Future<List<SkillDto>> listSkills({
    @Query('search') String? search,
  });

  @POST('/skills')
  Future<SkillDto> createSkill({
    @Body() CreateSkillRequest? body,
  });

  @PUT('/skills/{id}')
  Future<SkillDto> updateSkill({
    @Path('id') required String id,
    @Body() UpdateSkillRequest? body,
  });

  @DELETE('/skills/{id}')
  Future<void> retireSkill({
    @Path('id') required String id,
  });

  @GET('/staff/{id}/skills')
  Future<List<StaffSkillDto>> listStaffSkills({
    @Path('id') required String id,
  });

  @POST('/staff/{id}/skills')
  Future<StaffSkillDto> grantStaffSkill({
    @Path('id') required String id,
    @Body() GrantStaffSkillRequest? body,
  });

  @DELETE('/staff/{staffId}/skills/{skillId}')
  Future<RevokeStaffSkillResponse> revokeStaffSkill({
    @Path('staffId') required String staffId,
    @Path('skillId') required String skillId,
  });
}
