// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'update_reorder_threshold_request.g.dart';

@JsonSerializable()
class UpdateReorderThresholdRequest {
  const UpdateReorderThresholdRequest({
    required this.reorderThreshold,
  });
  
  factory UpdateReorderThresholdRequest.fromJson(Map<String, Object?> json) => _$UpdateReorderThresholdRequestFromJson(json);
  
  @JsonKey(name: 'reorder_threshold')
  final int reorderThreshold;

  Map<String, Object?> toJson() => _$UpdateReorderThresholdRequestToJson(this);
}
