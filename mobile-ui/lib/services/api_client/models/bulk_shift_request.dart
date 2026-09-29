// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'bulk_shift_pattern_item.dart';

part 'bulk_shift_request.g.dart';

@JsonSerializable()
class BulkShiftRequest {
  const BulkShiftRequest({
    required this.wardId,
    required this.from,
    required this.to,
    required this.patterns,
    this.weekdays,
  });
  
  factory BulkShiftRequest.fromJson(Map<String, Object?> json) => _$BulkShiftRequestFromJson(json);
  
  @JsonKey(name: 'ward_id')
  final String wardId;
  final DateTime from;
  final DateTime to;
  final List<String>? weekdays;
  final List<BulkShiftPatternItem> patterns;

  Map<String, Object?> toJson() => _$BulkShiftRequestToJson(this);
}
