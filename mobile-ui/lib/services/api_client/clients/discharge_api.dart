// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/checklist_update_request.dart';
import '../models/confirm_discharge_request.dart';
import '../models/discharge.dart';
import '../models/discharge_candidate_paged_result.dart';

part 'discharge_api.g.dart';

@RestApi()
abstract class DischargeApi {
  factory DischargeApi(Dio dio, {String? baseUrl}) = _DischargeApi;

  @GET('/discharges/candidates')
  Future<DischargeCandidatePagedResult> listDischargeCandidates({
    @Query('includeDischarged') bool? includeDischarged = false,
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('wardId') String? wardId,
  });

  @PATCH('/discharges/{admissionId}/checklist')
  Future<Discharge> updateDischargeChecklist({
    @Path('admissionId') required String admissionId,
    @Body() ChecklistUpdateRequest? body,
  });

  @POST('/discharges/{admissionId}/confirm')
  Future<Discharge> confirmDischarge({
    @Path('admissionId') required String admissionId,
    @Body() ConfirmDischargeRequest? body,
  });
}
