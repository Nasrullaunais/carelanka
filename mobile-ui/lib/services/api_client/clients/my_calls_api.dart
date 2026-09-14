// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/call_status.dart';
import '../models/my_emergency_call_summary_paged_result.dart';

part 'my_calls_api.g.dart';

@RestApi()
abstract class MyCallsApi {
  factory MyCallsApi(Dio dio, {String? baseUrl}) = _MyCallsApi;

  @GET('/me/emergency-calls')
  Future<MyEmergencyCallSummaryPagedResult> getMyEmergencyCalls({
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('status') CallStatus? status,
  });
}
