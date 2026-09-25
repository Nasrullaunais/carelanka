// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'dispatch_proposal_error.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DispatchProposalError _$DispatchProposalErrorFromJson(
  Map<String, dynamic> json,
) => DispatchProposalError(
  step: json['step'] as String?,
  message: json['message'] as String?,
  occurredAt: json['occurred_at'] == null
      ? null
      : DateTime.parse(json['occurred_at'] as String),
);

Map<String, dynamic> _$DispatchProposalErrorToJson(
  DispatchProposalError instance,
) => <String, dynamic>{
  'step': instance.step,
  'message': instance.message,
  'occurred_at': instance.occurredAt?.toIso8601String(),
};
