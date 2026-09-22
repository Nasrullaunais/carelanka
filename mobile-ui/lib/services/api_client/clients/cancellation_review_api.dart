// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/cancellation_request_status.dart';
import '../models/emergency_cancellation_request.dart';
import '../models/emergency_cancellation_request_paged_result.dart';
import '../models/review_cancellation_request.dart';

part 'cancellation_review_api.g.dart';

@RestApi()
abstract class CancellationReviewApi {
  factory CancellationReviewApi(Dio dio, {String? baseUrl}) = _CancellationReviewApi;

  @GET('/emergency-cancellation-requests')
  Future<EmergencyCancellationRequestPagedResult> listEmergencyCancellationRequests({
    @Query('Status') CancellationRequestStatus? status,
    @Query('Page') int? page,
    @Query('PageSize') int? pageSize,
  });

  @POST('/emergency-calls/{id}/cancellation-request/approve')
  Future<EmergencyCancellationRequest> approveEmergencyCancellationRequest({
    @Path('id') required String id,
    @Body() ReviewCancellationRequest? body,
  });

  @POST('/emergency-calls/{id}/cancellation-request/reject')
  Future<EmergencyCancellationRequest> rejectEmergencyCancellationRequest({
    @Path('id') required String id,
    @Body() ReviewCancellationRequest? body,
  });
}
