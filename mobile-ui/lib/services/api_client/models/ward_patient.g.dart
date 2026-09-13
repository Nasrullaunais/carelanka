// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ward_patient.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

WardPatient _$WardPatientFromJson(Map<String, dynamic> json) => WardPatient(
  admissionId: json['admission_id'] as String,
  patientId: json['patient_id'] as String,
  patientCode: json['patient_code'] as String,
  fullName: json['full_name'] as String,
  admissionStatus: AdmissionStatus.fromJson(json['admission_status'] as String),
  wardName: json['ward_name'] as String?,
  bedNumber: json['bed_number'] as String?,
);

Map<String, dynamic> _$WardPatientToJson(WardPatient instance) =>
    <String, dynamic>{
      'admission_id': instance.admissionId,
      'patient_id': instance.patientId,
      'patient_code': instance.patientCode,
      'full_name': instance.fullName,
      'ward_name': instance.wardName,
      'bed_number': instance.bedNumber,
      'admission_status': instance.admissionStatus,
    };
