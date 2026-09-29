// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'end_at_scene_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

EndAtSceneRequest _$EndAtSceneRequestFromJson(Map<String, dynamic> json) =>
    EndAtSceneRequest(
      outcome: json['outcome'] == null
          ? null
          : SceneOutcome.fromJson(json['outcome'] as String),
      notes: json['notes'] as String?,
    );

Map<String, dynamic> _$EndAtSceneRequestToJson(EndAtSceneRequest instance) =>
    <String, dynamic>{'outcome': instance.outcome, 'notes': instance.notes};
