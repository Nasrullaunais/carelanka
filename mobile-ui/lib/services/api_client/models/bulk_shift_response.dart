// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'shift_summary_dto.dart';

part 'bulk_shift_response.g.dart';

@JsonSerializable()
class BulkShiftResponse {
  const BulkShiftResponse({
    required this.created,
    required this.skipped,
    required this.shifts,
  });
  
  factory BulkShiftResponse.fromJson(Map<String, Object?> json) => _$BulkShiftResponseFromJson(json);
  
  final int created;
  final int skipped;
  final List<ShiftSummaryDto> shifts;

  Map<String, Object?> toJson() => _$BulkShiftResponseToJson(this);
}
