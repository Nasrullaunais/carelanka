// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'cancel_emergency_call_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CancelEmergencyCallRequest _$CancelEmergencyCallRequestFromJson(
  Map<String, dynamic> json,
) => CancelEmergencyCallRequest(
  outcome: json['outcome'] == null
      ? null
      : EmergencyCallOutcome.fromJson(json['outcome'] as String),
  notes: json['notes'] as String?,
);

Map<String, dynamic> _$CancelEmergencyCallRequestToJson(
  CancelEmergencyCallRequest instance,
) => <String, dynamic>{'outcome': instance.outcome, 'notes': instance.notes};
