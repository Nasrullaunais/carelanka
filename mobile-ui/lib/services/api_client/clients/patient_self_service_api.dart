// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/book_appointment_request.dart';
import '../models/claim_by_patient_code_request.dart';
import '../models/my_admission.dart';
import '../models/my_admission_paged_result.dart';
import '../models/my_appointment.dart';
import '../models/my_appointment_paged_result.dart';
import '../models/my_bill.dart';
import '../models/my_profile.dart';
import '../models/patient_claim_preview.dart';
import '../models/pre_register_request.dart';

part 'patient_self_service_api.g.dart';

@RestApi()
abstract class PatientSelfServiceApi {
  factory PatientSelfServiceApi(Dio dio, {String? baseUrl}) = _PatientSelfServiceApi;

  @POST('/me/pre-register')
  Future<MyProfile> preRegisterSelf({
    @Body() PreRegisterRequest? body,
  });

  @POST('/me/claim/preview')
  Future<PatientClaimPreview> previewMyClaim({
    @Body() ClaimByPatientCodeRequest? body,
  });

  @POST('/me/claim')
  Future<MyProfile> claimMyRecord({
    @Body() ClaimByPatientCodeRequest? body,
  });

  @GET('/me/profile')
  Future<MyProfile> getMyProfile();

  @GET('/me/admission')
  Future<MyAdmission> getMyAdmission();

  @GET('/me/history')
  Future<MyAdmissionPagedResult> getMyHistory({
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
  });

  @GET('/me/admissions/{admissionId}/bill')
  Future<MyBill> getMyBill({
    @Path('admissionId') required String admissionId,
  });

  @POST('/me/appointments')
  Future<MyAppointment> bookMyAppointment({
    @Body() BookAppointmentRequest? body,
  });

  @GET('/me/appointments')
  Future<MyAppointmentPagedResult> listMyAppointments({
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
  });

  @POST('/me/appointments/{id}/cancel')
  Future<MyAppointment> cancelMyAppointment({
    @Path('id') required String id,
  });
}
