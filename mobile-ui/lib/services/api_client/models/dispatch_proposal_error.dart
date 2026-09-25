// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'dispatch_proposal_error.g.dart';

@JsonSerializable()
class DispatchProposalError {
  const DispatchProposalError({this.step, this.message, this.occurredAt});

  factory DispatchProposalError.fromJson(Map<String, Object?> json) =>
      _$DispatchProposalErrorFromJson(json);

  final String? step;
  final String? message;
  @JsonKey(name: 'occurred_at')
  final DateTime? occurredAt;

  Map<String, Object?> toJson() => _$DispatchProposalErrorToJson(this);
}
