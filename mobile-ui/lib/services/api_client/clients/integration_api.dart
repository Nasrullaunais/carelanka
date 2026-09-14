// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/ward_capacity_summary.dart';

part 'integration_api.g.dart';

@RestApi()
abstract class IntegrationApi {
  factory IntegrationApi(Dio dio, {String? baseUrl}) = _IntegrationApi;

  @GET('/capacity/wards')
  Future<WardCapacitySummary> getWardCapacity();
}
