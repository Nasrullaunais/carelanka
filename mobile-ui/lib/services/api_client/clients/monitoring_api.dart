// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/warning.dart';
import '../models/warning_paged_result.dart';
import '../models/warning_severity.dart';
import '../models/warning_status.dart';
import '../models/warning_sweep_result.dart';
import '../models/warning_type.dart';

part 'monitoring_api.g.dart';

@RestApi()
abstract class MonitoringApi {
  factory MonitoringApi(Dio dio, {String? baseUrl}) = _MonitoringApi;

  @GET('/warnings')
  Future<WarningPagedResult> listWarnings({
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('status') WarningStatus? status,
    @Query('severity') WarningSeverity? severity,
    @Query('type') WarningType? type,
  });

  @POST('/warnings/sweep')
  Future<WarningSweepResult> runWarningSweep();

  @POST('/warnings/{id}/acknowledge')
  Future<Warning> acknowledgeWarning({
    @Path('id') required String id,
  });

  @POST('/warnings/{id}/clear')
  Future<void> clearWarning({
    @Path('id') required String id,
    @Header('X-Confirmation-Code') required String xConfirmationCode,
  });
}
