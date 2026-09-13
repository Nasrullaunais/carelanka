// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/bed.dart';
import '../models/bed_condition.dart';
import '../models/bed_paged_result.dart';
import '../models/create_bed_request.dart';
import '../models/update_bed_request.dart';

part 'beds_api.g.dart';

@RestApi()
abstract class BedsApi {
  factory BedsApi(Dio dio, {String? baseUrl}) = _BedsApi;

  @GET('/beds')
  Future<BedPagedResult> listBeds({
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('wardId') String? wardId,
    @Query('condition') BedCondition? condition,
  });

  @POST('/beds')
  Future<Bed> createBed({
    @Body() CreateBedRequest? body,
  });

  @PATCH('/beds/{id}')
  Future<Bed> updateBed({
    @Path('id') required String id,
    @Body() UpdateBedRequest? body,
  });

  @POST('/beds/{id}/retire')
  Future<Bed> retireBed({
    @Path('id') required String id,
  });
}
