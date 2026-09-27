// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'staff_role.dart';

part 'create_shift_request.g.dart';

@JsonSerializable()
class CreateShiftRequest {
  const CreateShiftRequest({
    required this.wardId,
    required this.date,
    required this.startTime,
    required this.endTime,
    required this.requiredRole,
    required this.headcountNeeded,
    this.requiredSkillId,
    this.minimumHeadcount,
  });
  
  factory CreateShiftRequest.fromJson(Map<String, Object?> json) => _$CreateShiftRequestFromJson(json);
  
  @JsonKey(name: 'ward_id')
  final String wardId;
  final DateTime date;
  @JsonKey(name: 'start_time')
  final String startTime;
  @JsonKey(name: 'end_time')
  final String endTime;
  @JsonKey(name: 'required_role')
  final StaffRole requiredRole;
  @JsonKey(name: 'required_skill_id')
  final String? requiredSkillId;
  @JsonKey(name: 'headcount_needed')
  final int headcountNeeded;
  @JsonKey(name: 'minimum_headcount')
  final int? minimumHeadcount;

  Map<String, Object?> toJson() => _$CreateShiftRequestToJson(this);
}
