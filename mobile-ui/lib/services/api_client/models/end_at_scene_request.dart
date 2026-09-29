// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'scene_outcome.dart';

part 'end_at_scene_request.g.dart';

@JsonSerializable()
class EndAtSceneRequest {
  const EndAtSceneRequest({
    this.outcome,
    this.notes,
  });
  
  factory EndAtSceneRequest.fromJson(Map<String, Object?> json) => _$EndAtSceneRequestFromJson(json);
  
  final SceneOutcome? outcome;
  final String? notes;

  Map<String, Object?> toJson() => _$EndAtSceneRequestToJson(this);
}
