// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/call_priority.dart';
import '../models/coverage_report.dart';
import '../models/emergency_agent_performance_report.dart';
import '../models/fleet_utilisation_report.dart';
import '../models/leave_report.dart';
import '../models/response_time_report.dart';
import '../models/staff_agent_performance_report.dart';

part 'reports_api.g.dart';

@RestApi()
abstract class ReportsApi {
  factory ReportsApi(Dio dio, {String? baseUrl}) = _ReportsApi;

  @GET('/reports/coverage')
  Future<CoverageReport> getCoverageReport({
    @Query('from') DateTime? from,
    @Query('to') DateTime? to,
    @Query('wardId') String? wardId,
  });

  @GET('/reports/leave')
  Future<LeaveReport> getLeaveReport({
    @Query('from') DateTime? from,
    @Query('to') DateTime? to,
    @Query('groupBy') String? groupBy,
  });

  @GET('/reports/staff/agent-performance')
  Future<StaffAgentPerformanceReport> getStaffAgentPerformanceReport({
    @Query('from') DateTime? from,
    @Query('to') DateTime? to,
  });

  @GET('/reports/emergency/response-times')
  Future<ResponseTimeReport> getEmergencyResponseTimeReport({
    @Query('from') required DateTime from,
    @Query('to') required DateTime to,
    @Query('priority') CallPriority? priority,
  });

  @GET('/reports/emergency/fleet-utilisation')
  Future<FleetUtilisationReport> getFleetUtilisationReport({
    @Query('from') required DateTime from,
    @Query('to') required DateTime to,
  });

  @GET('/reports/emergency/agent-performance')
  Future<EmergencyAgentPerformanceReport> getEmergencyAgentPerformanceReport({
    @Query('from') required DateTime from,
    @Query('to') required DateTime to,
  });
}
