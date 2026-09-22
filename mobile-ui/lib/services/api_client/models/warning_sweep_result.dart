// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'warning_sweep_result.g.dart';

@JsonSerializable()
class WarningSweepResult {
  const WarningSweepResult({
    required this.raised,
    required this.updated,
    required this.resolved,
    required this.stillOpen,
    required this.ranAt,
  });
  
  factory WarningSweepResult.fromJson(Map<String, Object?> json) => _$WarningSweepResultFromJson(json);
  
  final int raised;
  final int updated;
  final int resolved;
  @JsonKey(name: 'still_open')
  final int stillOpen;
  @JsonKey(name: 'ran_at')
  final DateTime ranAt;

  Map<String, Object?> toJson() => _$WarningSweepResultToJson(this);
}
