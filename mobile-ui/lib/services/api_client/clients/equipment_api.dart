// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/assign_equipment_item_request.dart';
import '../models/create_equipment_category_request.dart';
import '../models/create_equipment_item_request.dart';
import '../models/equipment_category.dart';
import '../models/equipment_item.dart';
import '../models/equipment_item_detail.dart';
import '../models/equipment_item_summary_paged_result.dart';
import '../models/equipment_status.dart';
import '../models/pending_equipment_count.dart';
import '../models/report_fault_request.dart';
import '../models/update_equipment_item_request.dart';

part 'equipment_api.g.dart';

@RestApi()
abstract class EquipmentApi {
  factory EquipmentApi(Dio dio, {String? baseUrl}) = _EquipmentApi;

  @GET('/equipment-categories')
  Future<List<EquipmentCategory>> listEquipmentCategories();

  @POST('/equipment-categories')
  Future<EquipmentCategory> createEquipmentCategory({
    @Body() CreateEquipmentCategoryRequest? body,
  });

  @GET('/equipment-items')
  Future<EquipmentItemSummaryPagedResult> listEquipmentItems({
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('sortBy') String? sortBy = 'created_at',
    @Query('sortDir') String? sortDir = 'desc',
    @Query('search') String? search,
    @Query('categoryId') String? categoryId,
    @Query('wardId') String? wardId,
    @Query('status') EquipmentStatus? status,
  });

  @POST('/equipment-items')
  Future<EquipmentItem> createEquipmentItem({
    @Body() CreateEquipmentItemRequest? body,
  });

  @GET('/equipment-items/pending-confirmation')
  Future<List<EquipmentItem>> listEquipmentItemsAwaitingConfirmation({
    @Header('X-Confirmation-Code') required String xConfirmationCode,
  });

  @GET('/equipment-items/pending-confirmation/count')
  Future<PendingEquipmentCount> countEquipmentItemsAwaitingConfirmation();

  @POST('/equipment-items/{id}/confirm')
  Future<EquipmentItem> confirmEquipmentItem({
    @Path('id') required String id,
    @Header('X-Confirmation-Code') required String xConfirmationCode,
  });

  @POST('/equipment-items/{id}/reject')
  Future<void> rejectEquipmentItem({
    @Path('id') required String id,
    @Header('X-Confirmation-Code') required String xConfirmationCode,
  });

  @GET('/equipment-items/{id}')
  Future<EquipmentItemDetail> getEquipmentItem({
    @Path('id') required String id,
  });

  @PUT('/equipment-items/{id}')
  Future<EquipmentItem> updateEquipmentItem({
    @Path('id') required String id,
    @Body() UpdateEquipmentItemRequest? body,
  });

  @DELETE('/equipment-items/{id}')
  Future<void> removeEquipmentItem({
    @Path('id') required String id,
    @Header('X-Confirmation-Code') required String xConfirmationCode,
  });

  @GET('/equipment-items/by-tag/{assetTag}')
  Future<EquipmentItemDetail> getEquipmentItemByTag({
    @Path('assetTag') required String assetTag,
  });

  @POST('/equipment-items/{id}/assign')
  Future<EquipmentItem> assignEquipmentItem({
    @Path('id') required String id,
    @Body() AssignEquipmentItemRequest? body,
  });

  @POST('/equipment-items/{id}/release')
  Future<EquipmentItem> releaseEquipmentItem({
    @Path('id') required String id,
  });

  @POST('/equipment-items/{id}/retire')
  Future<EquipmentItem> retireEquipmentItem({
    @Path('id') required String id,
    @Header('X-Confirmation-Code') required String xConfirmationCode,
  });

  @POST('/equipment-items/{id}/report-fault')
  Future<EquipmentItem> reportEquipmentFault({
    @Path('id') required String id,
    @Body() ReportFaultRequest? body,
  });
}
