// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/book_appointment_request.dart';
import '../models/my_admission.dart';
import '../models/my_admission_paged_result.dart';
import '../models/my_appointment.dart';
import '../models/my_appointment_paged_result.dart';
import '../models/my_profile.dart';
import '../models/pre_register_request.dart';

part 'patient_self_service_api.g.dart';

@RestApi()
abstract class PatientSelfServiceApi {
  factory PatientSelfServiceApi(Dio dio, {String? baseUrl}) = _PatientSelfServiceApi;

  @POST('/me/pre-register')
  Future<MyProfile> preRegisterSelf({
    @Body() PreRegisterRequest? body,
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
