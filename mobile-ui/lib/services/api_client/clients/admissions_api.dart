// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/admission.dart';
import '../models/admission_category.dart';
import '../models/admission_detail.dart';
import '../models/admission_sort_field.dart';
import '../models/admission_source.dart';
import '../models/admission_status.dart';
import '../models/admission_summary_paged_result.dart';
import '../models/appointment.dart';
import '../models/appointment_paged_result.dart';
import '../models/appointment_status.dart';
import '../models/assign_bed_request.dart';
import '../models/bed_assignment.dart';
import '../models/cancel_admission_request.dart';
import '../models/cancel_appointment_request.dart';
import '../models/check_in_request.dart';
import '../models/classify_admission_request.dart';
import '../models/complete_details_request.dart';
import '../models/correct_bed_request.dart';
import '../models/create_admission_request.dart';
import '../models/create_appointment_request.dart';
import '../models/pre_admit_request.dart';
import '../models/sort_direction.dart';
import '../models/worklist_row_paged_result.dart';

part 'admissions_api.g.dart';

@RestApi()
abstract class AdmissionsApi {
  factory AdmissionsApi(Dio dio, {String? baseUrl}) = _AdmissionsApi;

  @GET('/admissions')
  Future<AdmissionSummaryPagedResult> listAdmissions({
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('status') List<AdmissionStatus>? status,
    @Query('category') AdmissionCategory? category,
    @Query('source') AdmissionSource? source,
    @Query('detailsComplete') bool? detailsComplete,
    @Query('search') String? search,
    @Query('sortBy') AdmissionSortField? sortBy,
    @Query('sortDir') SortDirection? sortDir,
  });

  @POST('/admissions')
  Future<Admission> createAdmission({
    @Body() CreateAdmissionRequest? body,
  });

  @GET('/admissions/{id}')
  Future<AdmissionDetail> getAdmission({
    @Path('id') required String id,
  });

  @POST('/admissions/pre-admit')
  Future<Admission> preAdmitFromDispatch({
    @Body() PreAdmitRequest? body,
  });

  @POST('/admissions/{id}/classify')
  Future<Admission> classifyAdmission({
    @Path('id') required String id,
    @Body() ClassifyAdmissionRequest? body,
  });

  @PATCH('/admissions/{id}/details')
  Future<Admission> completeAdmissionDetails({
    @Path('id') required String id,
    @Body() CompleteDetailsRequest? body,
  });

  @POST('/admissions/{id}/arrive')
  Future<Admission> markArrived({
    @Path('id') required String id,
  });

  @POST('/admissions/{id}/cancel')
  Future<Admission> cancelAdmission({
    @Path('id') required String id,
    @Body() CancelAdmissionRequest? body,
  });

  @POST('/admissions/{id}/assign-bed')
  Future<BedAssignment> assignBedManually({
    @Path('id') required String id,
    @Body() AssignBedRequest? body,
  });

  @POST('/admissions/{id}/correct-bed')
  Future<BedAssignment> correctBed({
    @Path('id') required String id,
    @Body() CorrectBedRequest? body,
  });

  @GET('/appointments')
  Future<AppointmentPagedResult> listAppointments({
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('date') DateTime? date,
    @Query('status') AppointmentStatus? status,
  });

  @POST('/appointments')
  Future<Appointment> createAppointment({
    @Body() CreateAppointmentRequest? body,
  });

  @POST('/appointments/{id}/confirm')
  Future<Appointment> confirmAppointment({
    @Path('id') required String id,
  });

  @POST('/appointments/{id}/no-show')
  Future<Appointment> markAppointmentNoShow({
    @Path('id') required String id,
  });

  @POST('/appointments/{id}/check-in')
  Future<Admission> checkInAppointment({
    @Path('id') required String id,
    @Body() CheckInRequest? body,
  });

  @POST('/appointments/{id}/cancel')
  Future<Appointment> cancelAppointmentAtTheDesk({
    @Path('id') required String id,
    @Body() CancelAppointmentRequest? body,
  });

  @POST('/appointments/{id}/complete')
  Future<Appointment> completeAppointment({
    @Path('id') required String id,
  });

  @GET('/patient-worklist')
  Future<WorklistRowPagedResult> listPatientWorklist({
    @Query('includeFinished') bool? includeFinished = false,
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('search') String? search,
  });
}
