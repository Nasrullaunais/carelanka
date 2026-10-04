// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'close_run_at_scene_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CloseRunAtSceneRequest _$CloseRunAtSceneRequestFromJson(
  Map<String, dynamic> json,
) => CloseRunAtSceneRequest(
  outcome: json['outcome'] == null
      ? null
      : EmergencyCallOutcome.fromJson(json['outcome'] as String),
  notes: json['notes'] as String?,
);

Map<String, dynamic> _$CloseRunAtSceneRequestToJson(
  CloseRunAtSceneRequest instance,
) => <String, dynamic>{'outcome': instance.outcome, 'notes': instance.notes};
