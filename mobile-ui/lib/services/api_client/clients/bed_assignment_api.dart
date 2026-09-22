// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/bed_suggestion_request.dart';
import '../models/bed_workflow_accepted.dart';
import '../models/bed_workflow_summary.dart';

part 'bed_assignment_api.g.dart';

@RestApi()
abstract class BedAssignmentApi {
  factory BedAssignmentApi(Dio dio, {String? baseUrl}) = _BedAssignmentApi;

  @POST('/bed-suggestions')
  Future<BedWorkflowAccepted> requestBedSuggestion({
    @Body() BedSuggestionRequest? body,
  });

  @GET('/bed-workflows/{workflowId}')
  Future<BedWorkflowSummary> getBedWorkflow({
    @Path('workflowId') required String workflowId,
  });
}
