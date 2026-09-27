// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'roster_proposal_error_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

RosterProposalErrorDto _$RosterProposalErrorDtoFromJson(
  Map<String, dynamic> json,
) => RosterProposalErrorDto(
  step: json['step'] as String?,
  message: json['message'] as String?,
  occurredAt: json['occurred_at'] == null
      ? null
      : DateTime.parse(json['occurred_at'] as String),
);

Map<String, dynamic> _$RosterProposalErrorDtoToJson(
  RosterProposalErrorDto instance,
) => <String, dynamic>{
  'step': instance.step,
  'message': instance.message,
  'occurred_at': instance.occurredAt?.toIso8601String(),
};
