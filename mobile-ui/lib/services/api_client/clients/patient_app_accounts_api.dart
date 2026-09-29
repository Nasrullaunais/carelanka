// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/patient_app_account_paged_result.dart';
import '../models/patient_app_password_reset.dart';

part 'patient_app_accounts_api.g.dart';

@RestApi()
abstract class PatientAppAccountsApi {
  factory PatientAppAccountsApi(Dio dio, {String? baseUrl}) = _PatientAppAccountsApi;

  @GET('/patient-accounts')
  Future<PatientAppAccountPagedResult> listPatientAppAccounts({
    @Query('search') String? search,
    @Query('page') int? page,
    @Query('pageSize') int? pageSize,
  });

  @POST('/patient-accounts/{patientId}/reset-password')
  Future<PatientAppPasswordReset> resetPatientAppPassword({
    @Path('patientId') required String patientId,
  });
}
