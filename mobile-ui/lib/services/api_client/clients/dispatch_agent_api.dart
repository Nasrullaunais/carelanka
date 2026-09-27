// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/approve_dispatch_proposal_request.dart';
import '../models/create_dispatch_proposal_request.dart';
import '../models/dispatch_proposal_detail.dart';
import '../models/dispatch_proposal_status.dart';
import '../models/dispatch_proposal_summary.dart';
import '../models/dispatch_proposal_summary_paged_result.dart';
import '../models/reject_dispatch_proposal_request.dart';

part 'dispatch_agent_api.g.dart';

@RestApi()
abstract class DispatchAgentApi {
  factory DispatchAgentApi(Dio dio, {String? baseUrl}) = _DispatchAgentApi;

  @GET('/dispatch-proposals')
  Future<DispatchProposalSummaryPagedResult> listDispatchProposals({
    @Query('Status') DispatchProposalStatus? status,
    @Query('EmergencyCallId') String? emergencyCallId,
    @Query('IsDiversion') bool? isDiversion,
    @Query('Page') int? page,
    @Query('PageSize') int? pageSize,
  });

  @POST('/dispatch-proposals')
  Future<DispatchProposalSummary> createDispatchProposal({
    @Body() CreateDispatchProposalRequest? body,
  });

  @GET('/dispatch-proposals/{id}')
  Future<DispatchProposalDetail> getDispatchProposal({
    @Path('id') required String id,
  });

  @POST('/dispatch-proposals/{id}/confirm')
  Future<DispatchProposalDetail> confirmDispatchProposal({
    @Path('id') required String id,
  });

  @POST('/dispatch-proposals/{id}/approve')
  Future<DispatchProposalDetail> approveDispatchProposal({
    @Path('id') required String id,
    @Body() ApproveDispatchProposalRequest? body,
  });

  @POST('/dispatch-proposals/{id}/reject')
  Future<DispatchProposalDetail> rejectDispatchProposal({
    @Path('id') required String id,
    @Body() RejectDispatchProposalRequest? body,
  });
}
