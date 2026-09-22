// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/call_status.dart';
import '../models/emergency_cancellation_request.dart';
import '../models/my_call_tracking.dart';
import '../models/my_emergency_call_summary.dart';
import '../models/my_emergency_call_summary_paged_result.dart';
import '../models/request_cancellation_request.dart';

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

  @GET('/me/emergency-calls/{id}/tracking')
  Future<MyCallTracking> trackMyEmergencyCall({
    @Path('id') required String id,
  });

  @POST('/me/emergency-calls/{id}/cancel')
  Future<MyEmergencyCallSummary> cancelMyEmergencyCall({
    @Path('id') required String id,
    @Body() RequestCancellationRequest? body,
  });

  @POST('/me/emergency-calls/{id}/cancellation-request')
  Future<EmergencyCancellationRequest> requestMyEmergencyCallCancellation({
    @Path('id') required String id,
    @Body() RequestCancellationRequest? body,
  });
}
