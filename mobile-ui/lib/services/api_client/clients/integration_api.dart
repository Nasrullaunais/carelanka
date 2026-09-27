// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/lookup_staff_request.dart';
import '../models/staff_lookup_result.dart';
import '../models/ward_capacity_summary.dart';
import '../models/ward_coverage_overview_response.dart';

part 'integration_api.g.dart';

@RestApi()
abstract class IntegrationApi {
  factory IntegrationApi(Dio dio, {String? baseUrl}) = _IntegrationApi;

  @POST('/staff/lookup')
  Future<List<StaffLookupResult>> lookupStaff({
    @Body() LookupStaffRequest? body,
  });

  @GET('/coverage/wards')
  Future<WardCoverageOverviewResponse> getWardCoverage({
    @Query('at') DateTime? at,
  });

  @GET('/capacity/wards')
  Future<WardCapacitySummary> getWardCapacity();
}
