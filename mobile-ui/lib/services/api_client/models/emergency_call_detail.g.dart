// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'emergency_call_detail.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

EmergencyCallDetail _$EmergencyCallDetailFromJson(Map<String, dynamic> json) =>
    EmergencyCallDetail(
      id: json['id'] as String?,
      patientId: json['patient_id'] as String?,
      callerUserId: json['caller_user_id'] as String?,
      patientIsCaller: json['patient_is_caller'] as bool?,
      callerName: json['caller_name'] as String?,
      callerPhone: json['caller_phone'] as String?,
      latitude: (json['latitude'] as num?)?.toDouble(),
      longitude: (json['longitude'] as num?)?.toDouble(),
      locationAccuracyMetres: (json['location_accuracy_metres'] as num?)
          ?.toDouble(),
      locationCapturedAt: json['location_captured_at'] == null
          ? null
          : DateTime.parse(json['location_captured_at'] as String),
      idempotencyKey: json['idempotency_key'] as String?,
      addressLabel: json['address_label'] as String?,
      details: json['details'] as String?,
      priority: json['priority'] == null
          ? null
          : CallPriority.fromJson(json['priority'] as String),
      status: json['status'] == null
          ? null
          : CallStatus.fromJson(json['status'] as String),
      outcome: json['outcome'] == null
          ? null
          : EmergencyCallOutcome.fromJson(json['outcome'] as String),
      outcomeNotes: json['outcome_notes'] as String?,
      closedAt: json['closed_at'] == null
          ? null
          : DateTime.parse(json['closed_at'] as String),
      transported: json['transported'] as bool?,
      cancellationRequestStatus: json['cancellation_request_status'] == null
          ? null
          : CancellationRequestStatus.fromJson(
              json['cancellation_request_status'] as String,
            ),
      createdAt: json['created_at'] == null
          ? null
          : DateTime.parse(json['created_at'] as String),
      updatedAt: json['updated_at'] == null
          ? null
          : DateTime.parse(json['updated_at'] as String),
      patientName: json['patient_name'] as String?,
      dispatches: (json['dispatches'] as List<dynamic>?)
          ?.map((e) => DispatchDetail.fromJson(e as Map<String, dynamic>))
          .toList(),
      latestProposal: json['latest_proposal'] == null
          ? null
          : DispatchProposalSummary.fromJson(
              json['latest_proposal'] as Map<String, dynamic>,
            ),
    );

Map<String, dynamic> _$EmergencyCallDetailToJson(
  EmergencyCallDetail instance,
) => <String, dynamic>{
  'id': instance.id,
  'patient_id': instance.patientId,
  'caller_user_id': instance.callerUserId,
  'patient_is_caller': instance.patientIsCaller,
  'caller_name': instance.callerName,
  'caller_phone': instance.callerPhone,
  'latitude': instance.latitude,
  'longitude': instance.longitude,
  'location_accuracy_metres': instance.locationAccuracyMetres,
  'location_captured_at': instance.locationCapturedAt?.toIso8601String(),
  'idempotency_key': instance.idempotencyKey,
  'address_label': instance.addressLabel,
  'details': instance.details,
  'priority': instance.priority,
  'status': instance.status,
  'outcome': instance.outcome,
  'outcome_notes': instance.outcomeNotes,
  'closed_at': instance.closedAt?.toIso8601String(),
  'transported': instance.transported,
  'cancellation_request_status': instance.cancellationRequestStatus,
  'created_at': instance.createdAt?.toIso8601String(),
  'updated_at': instance.updatedAt?.toIso8601String(),
  'patient_name': instance.patientName,
  'dispatches': instance.dispatches,
  'latest_proposal': instance.latestProposal,
};
