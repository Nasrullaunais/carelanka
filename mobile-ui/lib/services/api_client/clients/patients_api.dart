// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/create_patient_request.dart';
import '../models/link_patient_account_request.dart';
import '../models/patient.dart';
import '../models/patient_detail.dart';
import '../models/patient_lookup_request.dart';
import '../models/patient_lookup_result.dart';
import '../models/patient_medical_profile.dart';
import '../models/patient_sort_field.dart';
import '../models/patient_summary_paged_result.dart';
import '../models/sort_direction.dart';
import '../models/update_medical_profile_request.dart';
import '../models/update_patient_request.dart';

part 'patients_api.g.dart';

@RestApi()
abstract class PatientsApi {
  factory PatientsApi(Dio dio, {String? baseUrl}) = _PatientsApi;

  @GET('/patients')
  Future<PatientSummaryPagedResult> listPatients({
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('search') String? search,
    @Query('sortBy') PatientSortField? sortBy,
    @Query('sortDir') SortDirection? sortDir,
  });

  @POST('/patients')
  Future<Patient> createPatient({
    @Body() CreatePatientRequest? body,
  });

  @GET('/patients/{id}')
  Future<PatientDetail> getPatient({
    @Path('id') required String id,
  });

  @PUT('/patients/{id}')
  Future<Patient> updatePatient({
    @Path('id') required String id,
    @Body() UpdatePatientRequest? body,
  });

  @POST('/patients/lookup')
  Future<PatientLookupResult> lookupPatient({
    @Body() PatientLookupRequest? body,
  });

  @GET('/patients/{id}/medical-profile')
  Future<PatientMedicalProfile> getPatientMedicalProfile({
    @Path('id') required String id,
  });

  @PUT('/patients/{id}/medical-profile')
  Future<PatientMedicalProfile> replacePatientMedicalProfile({
    @Path('id') required String id,
    @Body() UpdateMedicalProfileRequest? body,
  });

  @POST('/patients/{id}/link-account')
  Future<void> linkPatientAccount({
    @Path('id') required String id,
    @Body() LinkPatientAccountRequest? body,
  });
}
