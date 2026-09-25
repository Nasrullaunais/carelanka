// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/ambulance.dart';
import '../models/ambulance_crew_assignment.dart';
import '../models/ambulance_detail.dart';
import '../models/ambulance_sort_field.dart';
import '../models/ambulance_status.dart';
import '../models/ambulance_summary_paged_result.dart';
import '../models/assign_ambulance_crew_request.dart';
import '../models/create_ambulance_request.dart';
import '../models/report_ambulance_location_request.dart';
import '../models/retire_ambulance_request.dart';
import '../models/update_ambulance_request.dart';

part 'ambulances_api.g.dart';

@RestApi()
abstract class AmbulancesApi {
  factory AmbulancesApi(Dio dio, {String? baseUrl}) = _AmbulancesApi;

  @GET('/ambulances/{id}/crew')
  Future<List<AmbulanceCrewAssignment>> getCurrentAmbulanceCrew({
    @Path('id') required String id,
  });

  @POST('/ambulances/{id}/crew')
  Future<AmbulanceCrewAssignment> assignCurrentAmbulanceCrew({
    @Path('id') required String id,
    @Body() required AssignAmbulanceCrewRequest body,
  });

  @DELETE('/ambulances/{ambulanceId}/crew/{staffMemberId}')
  Future<void> unassignCurrentAmbulanceCrew({
    @Path('ambulanceId') required String ambulanceId,
    @Path('staffMemberId') required String staffMemberId,
  });

  @GET('/ambulances')
  Future<AmbulanceSummaryPagedResult> listAmbulances({
    @Query('status') AmbulanceStatus? status,
    @Query('search') String? search,
    @Query('nearToLatitude') double? nearToLatitude,
    @Query('nearToLongitude') double? nearToLongitude,
    @Query('includeRetired') bool? includeRetired,
    @Query('eligibleOnly') bool? eligibleOnly,
    @Query('page') int? page,
    @Query('pageSize') int? pageSize,
    @Query('sortBy') AmbulanceSortField? sortBy,
    @Query('sortDir') String? sortDir,
  });

  @POST('/ambulances')
  Future<Ambulance> createAmbulance({
    @Body() CreateAmbulanceRequest? body,
  });

  @GET('/ambulances/mine')
  Future<Ambulance> getMyAmbulanceAssignment();

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

  @POST('/ambulances/{id}/location')
  Future<void> reportAmbulanceLocation({
    @Path('id') required String id,
    @Body() ReportAmbulanceLocationRequest? body,
  });
}
