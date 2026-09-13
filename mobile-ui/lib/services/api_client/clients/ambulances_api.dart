// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/ambulance.dart';
import '../models/ambulance_detail.dart';
import '../models/ambulance_sort_field.dart';
import '../models/ambulance_status.dart';
import '../models/ambulance_summary_paged_result.dart';
import '../models/create_ambulance_request.dart';
import '../models/retire_ambulance_request.dart';
import '../models/update_ambulance_request.dart';

part 'ambulances_api.g.dart';

@RestApi()
abstract class AmbulancesApi {
  factory AmbulancesApi(Dio dio, {String? baseUrl}) = _AmbulancesApi;

  @GET('/ambulances')
  Future<AmbulanceSummaryPagedResult> listAmbulances({
    @Query('includeRetired') bool? includeRetired = false,
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('sortDir') String? sortDir = 'desc',
    @Query('status') AmbulanceStatus? status,
    @Query('search') String? search,
    @Query('nearToLatitude') double? nearToLatitude,
    @Query('nearToLongitude') double? nearToLongitude,
    @Query('sortBy') AmbulanceSortField? sortBy,
  });

  @POST('/ambulances')
  Future<Ambulance> createAmbulance({
    @Body() CreateAmbulanceRequest? body,
  });

  @GET('/ambulances/{id}')
  Future<AmbulanceDetail> getAmbulance({
    @Path('id') required String id,
  });

  @PATCH('/ambulances/{id}')
  Future<Ambulance> updateAmbulance({
    @Path('id') required String id,
    @Body() UpdateAmbulanceRequest? body,
  });

  @POST('/ambulances/{id}/retire')
  Future<void> retireAmbulance({
    @Path('id') required String id,
    @Body() RetireAmbulanceRequest? body,
  });

  @POST('/ambulances/{id}/reinstate')
  Future<Ambulance> reinstateAmbulance({
    @Path('id') required String id,
  });
}
