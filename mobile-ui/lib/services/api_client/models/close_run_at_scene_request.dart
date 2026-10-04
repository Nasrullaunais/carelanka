// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'emergency_call_outcome.dart';

part 'close_run_at_scene_request.g.dart';

@JsonSerializable()
class CloseRunAtSceneRequest {
  const CloseRunAtSceneRequest({
    this.outcome,
    this.notes,
  });
  
  factory CloseRunAtSceneRequest.fromJson(Map<String, Object?> json) => _$CloseRunAtSceneRequestFromJson(json);
  
  final EmergencyCallOutcome? outcome;
  final String? notes;

  Map<String, Object?> toJson() => _$CloseRunAtSceneRequestToJson(this);
}
