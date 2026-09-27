// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'classify_admission_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ClassifyAdmissionRequest _$ClassifyAdmissionRequestFromJson(
  Map<String, dynamic> json,
) => ClassifyAdmissionRequest(
  admissionCategory: json['admission_category'] == null
      ? null
      : AdmissionCategory.fromJson(json['admission_category'] as String),
);

Map<String, dynamic> _$ClassifyAdmissionRequestToJson(
  ClassifyAdmissionRequest instance,
) => <String, dynamic>{'admission_category': instance.admissionCategory};
