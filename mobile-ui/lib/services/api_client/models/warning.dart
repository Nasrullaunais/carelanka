// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'raised_by.dart';
import 'related_entity_type.dart';
import 'warning_severity.dart';
import 'warning_status.dart';
import 'warning_type.dart';

part 'warning.g.dart';

@JsonSerializable()
class Warning {
  const Warning({
    required this.id,
    required this.type,
    required this.severity,
    required this.relatedEntityType,
    required this.relatedEntityId,
    required this.recommendedAction,
    required this.status,
    required this.raisedBy,
    required this.createdAt,
    required this.updatedAt,
    this.wardId,
    this.workflowId,
    this.acknowledgedByStaffId,
    this.acknowledgedAt,
    this.resolvedAt,
  });
  
  factory Warning.fromJson(Map<String, Object?> json) => _$WarningFromJson(json);
  
  final String id;
  final WarningType type;
  final WarningSeverity severity;
  @JsonKey(name: 'related_entity_type')
  final RelatedEntityType relatedEntityType;
  @JsonKey(name: 'related_entity_id')
  final String relatedEntityId;
  @JsonKey(name: 'ward_id')
  final String? wardId;
  @JsonKey(name: 'recommended_action')
  final String recommendedAction;
  final WarningStatus status;
  @JsonKey(name: 'raised_by')
  final RaisedBy raisedBy;
  @JsonKey(name: 'workflow_id')
  final String? workflowId;
  @JsonKey(name: 'acknowledged_by_staff_id')
  final String? acknowledgedByStaffId;
  @JsonKey(name: 'acknowledged_at')
  final DateTime? acknowledgedAt;
  @JsonKey(name: 'resolved_at')
  final DateTime? resolvedAt;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  Map<String, Object?> toJson() => _$WarningToJson(this);
}
