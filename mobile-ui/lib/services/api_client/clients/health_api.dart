// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/health_status.dart';

part 'health_api.g.dart';

@RestApi()
abstract class HealthApi {
  factory HealthApi(Dio dio, {String? baseUrl}) = _HealthApi;

  @GET('/health')
  Future<HealthStatus> getHealth();
}
