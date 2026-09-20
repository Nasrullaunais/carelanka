// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'warning.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

Warning _$WarningFromJson(Map<String, dynamic> json) => Warning(
  id: json['id'] as String,
  type: WarningType.fromJson(json['type'] as String),
  severity: WarningSeverity.fromJson(json['severity'] as String),
  relatedEntityType: RelatedEntityType.fromJson(
    json['related_entity_type'] as String,
  ),
  relatedEntityId: json['related_entity_id'] as String,
  recommendedAction: json['recommended_action'] as String,
  status: WarningStatus.fromJson(json['status'] as String),
  raisedBy: RaisedBy.fromJson(json['raised_by'] as String),
  createdAt: DateTime.parse(json['created_at'] as String),
  updatedAt: DateTime.parse(json['updated_at'] as String),
  relatedEntityLabel: json['related_entity_label'] as String?,
  wardId: json['ward_id'] as String?,
  workflowId: json['workflow_id'] as String?,
  acknowledgedByStaffId: json['acknowledged_by_staff_id'] as String?,
  acknowledgedAt: json['acknowledged_at'] == null
      ? null
      : DateTime.parse(json['acknowledged_at'] as String),
  resolvedAt: json['resolved_at'] == null
      ? null
      : DateTime.parse(json['resolved_at'] as String),
);

Map<String, dynamic> _$WarningToJson(Warning instance) => <String, dynamic>{
  'id': instance.id,
  'type': instance.type,
  'severity': instance.severity,
  'related_entity_type': instance.relatedEntityType,
  'related_entity_id': instance.relatedEntityId,
  'related_entity_label': instance.relatedEntityLabel,
  'ward_id': instance.wardId,
  'recommended_action': instance.recommendedAction,
  'status': instance.status,
  'raised_by': instance.raisedBy,
  'workflow_id': instance.workflowId,
  'acknowledged_by_staff_id': instance.acknowledgedByStaffId,
  'acknowledged_at': instance.acknowledgedAt?.toIso8601String(),
  'resolved_at': instance.resolvedAt?.toIso8601String(),
  'created_at': instance.createdAt.toIso8601String(),
  'updated_at': instance.updatedAt.toIso8601String(),
};
