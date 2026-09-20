// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/decline_dispatch_request.dart';
import '../models/dispatch_detail.dart';
import '../models/navigation_target.dart';
import '../models/record_handover_request.dart';
import '../models/update_my_dispatch_status_request.dart';

part 'my_run_api.g.dart';

@RestApi()
abstract class MyRunApi {
  factory MyRunApi(Dio dio, {String? baseUrl}) = _MyRunApi;

  @GET('/me/dispatches/active')
  Future<DispatchDetail> getMyActiveDispatch();

  @GET('/me/dispatches/{id}/navigation')
  Future<NavigationTarget> getMyDispatchNavigationTarget({
    @Path('id') required String id,
  });

  @POST('/me/dispatches/{id}/acknowledge')
  Future<DispatchDetail> acknowledgeMyDispatch({
    @Path('id') required String id,
  });

  @POST('/me/dispatches/{id}/decline')
  Future<DispatchDetail> declineMyDispatch({
    @Path('id') required String id,
    @Body() DeclineDispatchRequest? body,
  });

  @POST('/me/dispatches/{id}/status')
  Future<DispatchDetail> updateMyDispatchStatus({
    @Path('id') required String id,
    @Body() UpdateMyDispatchStatusRequest? body,
  });

  @POST('/me/dispatches/{id}/handover')
  Future<DispatchDetail> recordHandover({
    @Path('id') required String id,
    @Body() RecordHandoverRequest? body,
  });
}
