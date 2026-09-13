// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_admission_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreateAdmissionRequest _$CreateAdmissionRequestFromJson(
  Map<String, dynamic> json,
) => CreateAdmissionRequest(
  patientId: json['patient_id'] as String,
  source: AdmissionSource.fromJson(json['source'] as String),
  admissionCategory: AdmissionCategory.fromJson(
    json['admission_category'] as String,
  ),
  categorySetByStaffId: json['category_set_by_staff_id'] as String,
  urgency: AdmissionUrgency.fromJson(json['urgency'] as String),
  isInfectious: json['is_infectious'] as bool? ?? false,
  dispatchId: json['dispatch_id'] as String?,
  expectedArrival: json['expected_arrival'] == null
      ? null
      : DateTime.parse(json['expected_arrival'] as String),
);

Map<String, dynamic> _$CreateAdmissionRequestToJson(
  CreateAdmissionRequest instance,
) => <String, dynamic>{
  'patient_id': instance.patientId,
  'source': instance.source,
  'dispatch_id': instance.dispatchId,
  'admission_category': instance.admissionCategory,
  'category_set_by_staff_id': instance.categorySetByStaffId,
  'urgency': instance.urgency,
  'is_infectious': instance.isInfectious,
  'expected_arrival': instance.expectedArrival?.toIso8601String(),
};
