// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/approve_care_recommendation_request.dart';
import '../models/care_recommendation.dart';
import '../models/care_recommendation_status.dart';
import '../models/care_recommendation_summary_paged_result.dart';
import '../models/care_workflow_summary.dart';
import '../models/reject_care_recommendation_request.dart';
import '../models/sort_direction.dart';

part 'care_recommendations_api.g.dart';

@RestApi()
abstract class CareRecommendationsApi {
  factory CareRecommendationsApi(Dio dio, {String? baseUrl}) = _CareRecommendationsApi;

  @GET('/care-workflows/{workflowId}')
  Future<CareWorkflowSummary> getCareWorkflow({
    @Path('workflowId') required String workflowId,
  });

  @GET('/care-recommendations')
  Future<CareRecommendationSummaryPagedResult> listCareRecommendations({
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('status') CareRecommendationStatus? status,
    @Query('sortDir') SortDirection? sortDir,
  });

  @GET('/care-recommendations/{id}')
  Future<CareRecommendation> getCareRecommendation({
    @Path('id') required String id,
  });

  @POST('/care-recommendations/{id}/approve')
  Future<CareRecommendation> approveCareRecommendation({
    @Path('id') required String id,
    @Body() ApproveCareRecommendationRequest? body,
  });

  @POST('/care-recommendations/{id}/reject')
  Future<CareRecommendation> rejectCareRecommendation({
    @Path('id') required String id,
    @Body() RejectCareRecommendationRequest? body,
  });
}
