// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'roster_proposal_error_dto.g.dart';

@JsonSerializable()
class RosterProposalErrorDto {
  const RosterProposalErrorDto({
    this.step,
    this.message,
    this.occurredAt,
  });
  
  factory RosterProposalErrorDto.fromJson(Map<String, Object?> json) => _$RosterProposalErrorDtoFromJson(json);
  
  final String? step;
  final String? message;
  @JsonKey(name: 'occurred_at')
  final DateTime? occurredAt;

  Map<String, Object?> toJson() => _$RosterProposalErrorDtoToJson(this);
}
