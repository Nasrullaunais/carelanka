// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/create_pharmacy_category_request.dart';
import '../models/create_pharmacy_item_request.dart';
import '../models/create_pharmacy_transaction_request.dart';
import '../models/pharmacy_category.dart';
import '../models/pharmacy_item.dart';
import '../models/pharmacy_item_paged_result.dart';
import '../models/pharmacy_transaction_paged_result.dart';

part 'pharmacy_api.g.dart';

@RestApi()
abstract class PharmacyApi {
  factory PharmacyApi(Dio dio, {String? baseUrl}) = _PharmacyApi;

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
}
