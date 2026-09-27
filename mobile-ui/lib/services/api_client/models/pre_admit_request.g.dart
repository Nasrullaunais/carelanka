// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'pre_admit_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

PreAdmitRequest _$PreAdmitRequestFromJson(Map<String, dynamic> json) =>
    PreAdmitRequest(
      dispatchId: json['dispatch_id'] as String?,
      patientIsCaller: json['patient_is_caller'] as bool?,
      callerUserId: json['caller_user_id'] as String?,
      patientId: json['patient_id'] as String?,
      expectedArrival: json['expected_arrival'] == null
          ? null
          : DateTime.parse(json['expected_arrival'] as String),
      urgency: json['urgency'] == null
          ? null
          : AdmissionUrgency.fromJson(json['urgency'] as String),
      destinationWardTypeHint: json['destination_ward_type_hint'] == null
          ? null
          : WardType.fromJson(json['destination_ward_type_hint'] as String),
      provisionalName: json['provisional_name'] as String?,
      provisionalGender: json['provisional_gender'] == null
          ? null
          : Gender.fromJson(json['provisional_gender'] as String),
    );

Map<String, dynamic> _$PreAdmitRequestToJson(PreAdmitRequest instance) =>
    <String, dynamic>{
      'dispatch_id': instance.dispatchId,
      'patient_is_caller': instance.patientIsCaller,
      'caller_user_id': instance.callerUserId,
      'patient_id': instance.patientId,
      'expected_arrival': instance.expectedArrival?.toIso8601String(),
      'urgency': instance.urgency,
      'destination_ward_type_hint': instance.destinationWardTypeHint,
      'provisional_name': instance.provisionalName,
      'provisional_gender': instance.provisionalGender,
    };
