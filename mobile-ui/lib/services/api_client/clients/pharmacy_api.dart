// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'dart:convert';
import 'dart:io';

import 'dart:typed_data';
import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/add_pharmacy_batch_request.dart';
import '../models/create_pharmacy_category_request.dart';
import '../models/create_pharmacy_item_request.dart';
import '../models/create_pharmacy_transaction_request.dart';
import '../models/my_prescription.dart';
import '../models/pharmacy_batch.dart';
import '../models/pharmacy_category.dart';
import '../models/pharmacy_item.dart';
import '../models/pharmacy_item_paged_result.dart';
import '../models/pharmacy_transaction_paged_result.dart';
import '../models/prescription.dart';
import '../models/prescription_status.dart';
import '../models/reject_prescription_request.dart';

part 'pharmacy_api.g.dart';

@RestApi()
abstract class PharmacyApi {
  factory PharmacyApi(Dio dio, {String? baseUrl}) = _PharmacyApi;

  @GET('/me/prescriptions')
  Future<List<MyPrescription>> listMyPrescriptions();

  @MultiPart()
  @POST('/me/prescriptions')
  Future<MyPrescription> uploadMyPrescription({
    @Part(name: 'File') File? file,
    @Part(name: 'Note') String? note,
  });

  @GET('/pharmacy-categories')
  Future<List<PharmacyCategory>> listPharmacyCategories();

  @POST('/pharmacy-categories')
  Future<PharmacyCategory> createPharmacyCategory({
    @Body() CreatePharmacyCategoryRequest? body,
  });

  @GET('/pharmacy-items')
  Future<PharmacyItemPagedResult> listPharmacyItems({
    @Query('availableOnly') bool? availableOnly = false,
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('sortBy') String? sortBy = 'name',
    @Query('sortDir') String? sortDir = 'asc',
    @Query('search') String? search,
    @Query('categoryId') String? categoryId,
  });

  @POST('/pharmacy-items')
  Future<PharmacyItem> createPharmacyItem({
    @Body() CreatePharmacyItemRequest? body,
  });

  @GET('/pharmacy-items/{id}')
  Future<PharmacyItem> getPharmacyItem({
    @Path('id') required String id,
  });

  @GET('/pharmacy-items/{id}/batches')
  Future<List<PharmacyBatch>> listPharmacyBatches({
    @Path('id') required String id,
  });

  @POST('/pharmacy-items/{id}/batches')
  Future<PharmacyBatch> addPharmacyBatch({
    @Path('id') required String id,
    @Body() AddPharmacyBatchRequest? body,
  });

  @POST('/pharmacy-items/{id}/transactions')
  Future<PharmacyItem> recordPharmacyTransaction({
    @Path('id') required String id,
    @Body() CreatePharmacyTransactionRequest? body,
  });

  @GET('/pharmacy-items/{id}/transactions')
  Future<PharmacyTransactionPagedResult> listPharmacyTransactions({
    @Path('id') required String id,
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
  });

  @POST('/pharmacy-items/{id}/batches/{batchId}/transactions')
  Future<PharmacyItem> recordPharmacyBatchTransaction({
    @Path('id') required String id,
    @Path('batchId') required String batchId,
    @Body() CreatePharmacyTransactionRequest? body,
  });

  @GET('/prescriptions')
  Future<List<Prescription>> listPrescriptions({
    @Query('status') PrescriptionStatus? status,
  });

  @GET('/prescriptions/{id}/file')
  @DioResponseType(ResponseType.stream)
  Stream<String> downloadPrescription({
    @Path('id') required String id,
  });

  @POST('/prescriptions/{id}/ready')
  Future<Prescription> markPrescriptionReady({
    @Path('id') required String id,
  });

  @POST('/prescriptions/{id}/deliver')
  Future<Prescription> markPrescriptionDelivered({
    @Path('id') required String id,
  });

  @POST('/prescriptions/{id}/reject')
  Future<Prescription> rejectPrescription({
    @Path('id') required String id,
    @Body() RejectPrescriptionRequest? body,
  });
}
