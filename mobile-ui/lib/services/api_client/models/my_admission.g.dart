// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'my_admission.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

MyAdmission _$MyAdmissionFromJson(Map<String, dynamic> json) => MyAdmission(
  admissionId: json['admission_id'] as String,
  status: AdmissionStatus.fromJson(json['status'] as String),
  statusText: json['status_text'] as String,
  detailsComplete: json['details_complete'] as bool,
  missingFields: (json['missing_fields'] as List<dynamic>)
      .map((e) => e as String)
      .toList(),
  wardName: json['ward_name'] as String?,
  bedNumber: json['bed_number'] as String?,
  admittedAt: json['admitted_at'] == null
      ? null
      : DateTime.parse(json['admitted_at'] as String),
  expectedArrival: json['expected_arrival'] == null
      ? null
      : DateTime.parse(json['expected_arrival'] as String),
  dischargedAt: json['discharged_at'] == null
      ? null
      : DateTime.parse(json['discharged_at'] as String),
  dischargeInstructions: json['discharge_instructions'] as String?,
);

Map<String, dynamic> _$MyAdmissionToJson(MyAdmission instance) =>
    <String, dynamic>{
      'admission_id': instance.admissionId,
      'status': instance.status,
      'status_text': instance.statusText,
      'ward_name': instance.wardName,
      'bed_number': instance.bedNumber,
      'admitted_at': instance.admittedAt?.toIso8601String(),
      'expected_arrival': instance.expectedArrival?.toIso8601String(),
      'discharged_at': instance.dischargedAt?.toIso8601String(),
      'discharge_instructions': instance.dischargeInstructions,
      'details_complete': instance.detailsComplete,
      'missing_fields': instance.missingFields,
    };
