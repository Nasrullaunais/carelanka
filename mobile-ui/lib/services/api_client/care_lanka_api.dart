// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';

import 'clients/admissions_api.dart';
import 'clients/ambulances_api.dart';
import 'clients/auth_api.dart';
import 'clients/beds_api.dart';
import 'clients/billing_api.dart';
import 'clients/calls_api.dart';
import 'clients/cancellation_review_api.dart';
import 'clients/discharge_api.dart';
import 'clients/dispatches_api.dart';
import 'clients/equipment_api.dart';
import 'clients/health_api.dart';
import 'clients/integration_api.dart';
import 'clients/laboratory_api.dart';
import 'clients/maintenance_api.dart';
import 'clients/monitoring_api.dart';
import 'clients/my_calls_api.dart';
import 'clients/my_run_api.dart';
import 'clients/patient_self_service_api.dart';
import 'clients/patients_api.dart';
import 'clients/pharmacy_api.dart';
import 'clients/wards_and_beds_api.dart';

/// CareLanka API `vv1`
class CareLankaApi {
  CareLankaApi(
    Dio dio, {
    String? baseUrl,
  })  : _dio = dio,
        _baseUrl = baseUrl;

  final Dio _dio;
  final String? _baseUrl;

  static String get version => 'v1';

  AdmissionsApi? _admissions;
  AmbulancesApi? _ambulances;
  AuthApi? _auth;
  BedsApi? _beds;
  BillingApi? _billing;
  CallsApi? _calls;
  CancellationReviewApi? _cancellationReview;
  DischargeApi? _discharge;
  DispatchesApi? _dispatches;
  EquipmentApi? _equipment;
  HealthApi? _health;
  IntegrationApi? _integration;
  LaboratoryApi? _laboratory;
  MaintenanceApi? _maintenance;
  MonitoringApi? _monitoring;
  MyCallsApi? _myCalls;
  MyRunApi? _myRun;
  PatientSelfServiceApi? _patientSelfService;
  PatientsApi? _patients;
  PharmacyApi? _pharmacy;
  WardsAndBedsApi? _wardsAndBeds;

  AdmissionsApi get admissions => _admissions ??= AdmissionsApi(_dio, baseUrl: _baseUrl);

  AmbulancesApi get ambulances => _ambulances ??= AmbulancesApi(_dio, baseUrl: _baseUrl);

  AuthApi get auth => _auth ??= AuthApi(_dio, baseUrl: _baseUrl);

  BedsApi get beds => _beds ??= BedsApi(_dio, baseUrl: _baseUrl);

  BillingApi get billing => _billing ??= BillingApi(_dio, baseUrl: _baseUrl);

  CallsApi get calls => _calls ??= CallsApi(_dio, baseUrl: _baseUrl);

  CancellationReviewApi get cancellationReview => _cancellationReview ??= CancellationReviewApi(_dio, baseUrl: _baseUrl);

  DischargeApi get discharge => _discharge ??= DischargeApi(_dio, baseUrl: _baseUrl);

  DispatchesApi get dispatches => _dispatches ??= DispatchesApi(_dio, baseUrl: _baseUrl);

  EquipmentApi get equipment => _equipment ??= EquipmentApi(_dio, baseUrl: _baseUrl);

  HealthApi get health => _health ??= HealthApi(_dio, baseUrl: _baseUrl);

  IntegrationApi get integration => _integration ??= IntegrationApi(_dio, baseUrl: _baseUrl);

  LaboratoryApi get laboratory => _laboratory ??= LaboratoryApi(_dio, baseUrl: _baseUrl);

  MaintenanceApi get maintenance => _maintenance ??= MaintenanceApi(_dio, baseUrl: _baseUrl);

  MonitoringApi get monitoring => _monitoring ??= MonitoringApi(_dio, baseUrl: _baseUrl);

  MyCallsApi get myCalls => _myCalls ??= MyCallsApi(_dio, baseUrl: _baseUrl);

  MyRunApi get myRun => _myRun ??= MyRunApi(_dio, baseUrl: _baseUrl);

  PatientSelfServiceApi get patientSelfService => _patientSelfService ??= PatientSelfServiceApi(_dio, baseUrl: _baseUrl);

  PatientsApi get patients => _patients ??= PatientsApi(_dio, baseUrl: _baseUrl);

  PharmacyApi get pharmacy => _pharmacy ??= PharmacyApi(_dio, baseUrl: _baseUrl);

  WardsAndBedsApi get wardsAndBeds => _wardsAndBeds ??= WardsAndBedsApi(_dio, baseUrl: _baseUrl);
}
