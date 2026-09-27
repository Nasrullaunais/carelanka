// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/approve_roster_proposal_request.dart';
import '../models/create_roster_proposal_request.dart';
import '../models/reject_roster_proposal_request.dart';
import '../models/request_roster_proposal_revision_request.dart';
import '../models/roster_proposal_detail.dart';
import '../models/roster_proposal_status.dart';
import '../models/roster_proposal_summary.dart';
import '../models/roster_proposal_summary_paged_result.dart';

part 'roster_agent_api.g.dart';

@RestApi()
abstract class RosterAgentApi {
  factory RosterAgentApi(Dio dio, {String? baseUrl}) = _RosterAgentApi;

  @GET('/roster-proposals')
  Future<RosterProposalSummaryPagedResult> listRosterProposals({
    @Query('status') RosterProposalStatus? status,
    @Query('wardId') String? wardId,
    @Query('shiftId') String? shiftId,
    @Query('page') int? page,
    @Query('pageSize') int? pageSize,
  });

  @POST('/roster-proposals')
  Future<RosterProposalSummary> createRosterProposal({
    @Body() CreateRosterProposalRequest? body,
  });

  @GET('/roster-proposals/{id}')
  Future<RosterProposalDetail> getRosterProposal({
    @Path('id') required String id,
  });

  @POST('/roster-proposals/{id}/approve')
  Future<RosterProposalDetail> approveRosterProposal({
    @Path('id') required String id,
    @Body() ApproveRosterProposalRequest? body,
  });

  @POST('/roster-proposals/{id}/reject')
  Future<RosterProposalDetail> rejectRosterProposal({
    @Path('id') required String id,
    @Body() RejectRosterProposalRequest? body,
  });

  @POST('/roster-proposals/{id}/request-revision')
  Future<RosterProposalSummary> reviseRosterProposal({
    @Path('id') required String id,
    @Body() RequestRosterProposalRevisionRequest? body,
  });
}
