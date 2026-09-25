// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'crew_candidate.g.dart';

@JsonSerializable()
class CrewCandidate {
  const CrewCandidate({
    required this.staffMemberId,
    required this.fullName,
  });
  
  factory CrewCandidate.fromJson(Map<String, Object?> json) => _$CrewCandidateFromJson(json);
  
  @JsonKey(name: 'staff_member_id')
  final String staffMemberId;
  @JsonKey(name: 'full_name')
  final String fullName;

  Map<String, Object?> toJson() => _$CrewCandidateToJson(this);
}
