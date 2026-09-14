// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/call_priority.dart';
import '../models/call_status.dart';
import '../models/create_emergency_call_request.dart';
import '../models/emergency_call_detail.dart';
import '../models/emergency_call_sort_field.dart';
import '../models/emergency_call_summary_paged_result.dart';
import '../models/sort_dir.dart';
import '../models/update_emergency_call_request.dart';

part 'calls_api.g.dart';

@RestApi()
abstract class CallsApi {
  factory CallsApi(Dio dio, {String? baseUrl}) = _CallsApi;

  @POST('/emergency-calls')
  Future<EmergencyCallDetail> createEmergencyCall({
    @Body() required CreateEmergencyCallRequest body,
  });

  @GET('/emergency-calls')
  Future<EmergencyCallSummaryPagedResult> listEmergencyCalls({
    @Query('unassignedOnly') bool? unassignedOnly = false,
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('sortDir') SortDir? sortDir = SortDir.desc,
    @Query('status') CallStatus? status,
    @Query('priority') CallPriority? priority,
    @Query('search') String? search,
    @Query('from') DateTime? from,
    @Query('to') DateTime? to,
    @Query('sortBy') EmergencyCallSortField? sortBy,
  });

  @GET('/emergency-calls/{id}')
  Future<EmergencyCallDetail> getEmergencyCall({
    @Path('id') required String id,
  });

  @PATCH('/emergency-calls/{id}')
  Future<EmergencyCallDetail> updateEmergencyCall({
    @Path('id') required String id,
    @Body() required UpdateEmergencyCallRequest body,
  });
}
