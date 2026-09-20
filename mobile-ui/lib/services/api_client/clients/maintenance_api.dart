// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/asset_type.dart';
import '../models/create_maintenance_schedule_request.dart';
import '../models/maintenance_schedule.dart';
import '../models/maintenance_schedule_paged_result.dart';
import '../models/maintenance_status.dart';
import '../models/pending_equipment_count.dart';

part 'maintenance_api.g.dart';

@RestApi()
abstract class MaintenanceApi {
  factory MaintenanceApi(Dio dio, {String? baseUrl}) = _MaintenanceApi;

  @GET('/maintenance-schedules')
  Future<MaintenanceSchedulePagedResult> listMaintenanceSchedules({
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('status') MaintenanceStatus? status,
    @Query('assetType') AssetType? assetType,
    @Query('overdue') bool? overdue,
  });

  @POST('/maintenance-schedules')
  Future<MaintenanceSchedule> createMaintenanceSchedule({
    @Body() CreateMaintenanceScheduleRequest? body,
  });

  @GET('/maintenance-schedules/pending-confirmation')
  Future<List<MaintenanceSchedule>> listMaintenanceSchedulesAwaitingConfirmation({
    @Header('X-Confirmation-Code') required String xConfirmationCode,
  });

  @GET('/maintenance-schedules/pending-confirmation/count')
  Future<PendingEquipmentCount> countMaintenanceSchedulesAwaitingConfirmation();

  @POST('/maintenance-schedules/{id}/confirm')
  Future<MaintenanceSchedule> confirmMaintenanceSchedule({
    @Path('id') required String id,
    @Header('X-Confirmation-Code') required String xConfirmationCode,
  });
}
