// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'emergency_call_summary.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

EmergencyCallSummary _$EmergencyCallSummaryFromJson(
  Map<String, dynamic> json,
) => EmergencyCallSummary(
  id: json['id'] as String?,
  priority: json['priority'] == null
      ? null
      : CallPriority.fromJson(json['priority'] as String),
  status: json['status'] == null
      ? null
      : CallStatus.fromJson(json['status'] as String),
  callerName: json['caller_name'] as String?,
  addressLabel: json['address_label'] as String?,
  latitude: (json['latitude'] as num?)?.toDouble(),
  longitude: (json['longitude'] as num?)?.toDouble(),
  activeDispatchId: json['active_dispatch_id'] as String?,
  openProposalId: json['open_proposal_id'] as String?,
  waitingMinutes: (json['waiting_minutes'] as num?)?.toInt(),
  createdAt: json['created_at'] == null
      ? null
      : DateTime.parse(json['created_at'] as String),
);

Map<String, dynamic> _$EmergencyCallSummaryToJson(
  EmergencyCallSummary instance,
) => <String, dynamic>{
  'id': instance.id,
  'priority': instance.priority,
  'status': instance.status,
  'caller_name': instance.callerName,
  'address_label': instance.addressLabel,
  'latitude': instance.latitude,
  'longitude': instance.longitude,
  'active_dispatch_id': instance.activeDispatchId,
  'open_proposal_id': instance.openProposalId,
  'waiting_minutes': instance.waitingMinutes,
  'created_at': instance.createdAt?.toIso8601String(),
};
