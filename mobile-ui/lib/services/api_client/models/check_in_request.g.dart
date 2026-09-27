// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'check_in_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CheckInRequest _$CheckInRequestFromJson(Map<String, dynamic> json) =>
    CheckInRequest(
      admissionCategory: AdmissionCategory.fromJson(
        json['admission_category'] as String,
      ),
      urgency: AdmissionUrgency.fromJson(json['urgency'] as String),
      isInfectious: json['is_infectious'] as bool? ?? false,
    );

Map<String, dynamic> _$CheckInRequestToJson(CheckInRequest instance) =>
    <String, dynamic>{
      'admission_category': instance.admissionCategory,
      'urgency': instance.urgency,
      'is_infectious': instance.isInfectious,
    };
