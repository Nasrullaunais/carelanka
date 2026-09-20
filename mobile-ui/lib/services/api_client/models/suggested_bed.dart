// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'suggested_bed.g.dart';

@JsonSerializable()
class SuggestedBed {
  const SuggestedBed({
    required this.bedId,
    required this.wardName,
    required this.bedNumber,
    required this.isDowngrade,
    required this.requiresDutyManager,
    this.rulesSatisfied,
    this.rationale,
  });
  
  factory SuggestedBed.fromJson(Map<String, Object?> json) => _$SuggestedBedFromJson(json);
  
  @JsonKey(name: 'bed_id')
  final String bedId;
  @JsonKey(name: 'ward_name')
  final String? wardName;
  @JsonKey(name: 'bed_number')
  final String? bedNumber;
  @JsonKey(name: 'is_downgrade')
  final bool isDowngrade;
  @JsonKey(name: 'requires_duty_manager')
  final bool requiresDutyManager;
  @JsonKey(name: 'rules_satisfied')
  final List<String>? rulesSatisfied;
  final String? rationale;

  Map<String, Object?> toJson() => _$SuggestedBedToJson(this);
}
