// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/crew_candidate.dart';

part 'staff_api.g.dart';

@RestApi()
abstract class StaffApi {
  factory StaffApi(Dio dio, {String? baseUrl}) = _StaffApi;

  @GET('/staff/crew-candidates')
  Future<List<CrewCandidate>> searchAvailableCrew({
    @Query('search') String? search,
  });
}
