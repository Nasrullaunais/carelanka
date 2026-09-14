// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'dart:convert';
import 'dart:io';

import 'dart:typed_data';
import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/lab_report.dart';
import '../models/lab_report_paged_result.dart';
import '../models/ward_patient_paged_result.dart';

part 'laboratory_api.g.dart';

@RestApi()
abstract class LaboratoryApi {
  factory LaboratoryApi(Dio dio, {String? baseUrl}) = _LaboratoryApi;

  @GET('/lab-reports')
  Future<LabReportPagedResult> listLabReports({
    @Query('patientId') required String patientId,
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
  });

  @MultiPart()
  @POST('/lab-reports')
  Future<LabReport> uploadLabReport({
    @Part(name: 'PatientId') required String patientId,
    @Part(name: 'TestName') required String testName,
    @Part(name: 'File') required File file,
    @Part(name: 'Summary') String? summary,
  });

  @GET('/lab-reports/{id}/file')
  @DioResponseType(ResponseType.stream)
  Stream<String> downloadLabReport({
    @Path('id') required String id,
  });

  @GET('/ward-patients')
  Future<WardPatientPagedResult> listWardPatients({
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('wardName') String? wardName,
  });
}
