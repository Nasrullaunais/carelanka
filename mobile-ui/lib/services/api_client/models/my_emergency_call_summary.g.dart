// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'my_emergency_call_summary.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

MyEmergencyCallSummary _$MyEmergencyCallSummaryFromJson(
  Map<String, dynamic> json,
) => MyEmergencyCallSummary(
  id: json['id'] as String?,
  patientIsCaller: json['patient_is_caller'] as bool?,
  priority: json['priority'] == null
      ? null
      : CallPriority.fromJson(json['priority'] as String),
  status: json['status'] == null
      ? null
      : CallStatus.fromJson(json['status'] as String),
  cancellationRequestStatus: json['cancellation_request_status'] == null
      ? null
      : CancellationRequestStatus.fromJson(
          json['cancellation_request_status'] as String,
        ),
  createdAt: json['created_at'] == null
      ? null
      : DateTime.parse(json['created_at'] as String),
);

Map<String, dynamic> _$MyEmergencyCallSummaryToJson(
  MyEmergencyCallSummary instance,
) => <String, dynamic>{
  'id': instance.id,
  'patient_is_caller': instance.patientIsCaller,
  'priority': instance.priority,
  'status': instance.status,
  'cancellation_request_status': instance.cancellationRequestStatus,
  'created_at': instance.createdAt?.toIso8601String(),
};
