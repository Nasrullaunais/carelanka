// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/admission_bed_paged_result.dart';
import '../models/bed_availability_filter.dart';
import '../models/bed_occupancy_status.dart';
import '../models/create_ward_request.dart';
import '../models/ward.dart';
import '../models/ward_occupancy.dart';
import '../models/ward_type.dart';

part 'wards_and_beds_api.g.dart';

@RestApi()
abstract class WardsAndBedsApi {
  factory WardsAndBedsApi(Dio dio, {String? baseUrl}) = _WardsAndBedsApi;

  @GET('/bed-availability')
  Future<AdmissionBedPagedResult> listBedAvailability({
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('wardId') String? wardId,
    @Query('wardType') WardType? wardType,
    @Query('needsIsolation') bool? needsIsolation,
    @Query('availability') BedAvailabilityFilter? availability,
  });

  @GET('/beds/{id}/occupancy')
  Future<BedOccupancyStatus> getBedOccupancy({
    @Path('id') required String id,
  });

  @GET('/wards')
  Future<List<Ward>> listWards({
    @Query('isActive') bool? isActive = true,
    @Query('wardType') WardType? wardType,
  });

  @POST('/wards')
  Future<Ward> createWard({
    @Body() CreateWardRequest? body,
  });

  @GET('/wards/{id}/occupancy')
  Future<WardOccupancy> getWardOccupancy({
    @Path('id') required String id,
  });
}
