// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/cancel_dispatch_request.dart';
import '../models/dispatch_detail.dart';
import '../models/reassign_dispatch_request.dart';
import '../models/route_log.dart';

part 'dispatches_api.g.dart';

@RestApi()
abstract class DispatchesApi {
  factory DispatchesApi(Dio dio, {String? baseUrl}) = _DispatchesApi;

  @GET('/dispatches/{id}/route')
  Future<RouteLog> getDispatchRoute({
    @Path('id') required String id,
  });

  @POST('/dispatches/{id}/cancel')
  Future<DispatchDetail> cancelDispatch({
    @Path('id') required String id,
    @Body() CancelDispatchRequest? body,
  });

  @POST('/dispatches/{id}/reassign')
  Future<DispatchDetail> reassignDispatch({
    @Path('id') required String id,
    @Body() ReassignDispatchRequest? body,
  });
}
