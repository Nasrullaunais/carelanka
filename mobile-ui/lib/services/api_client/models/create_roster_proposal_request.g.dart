// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_roster_proposal_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreateRosterProposalRequest _$CreateRosterProposalRequestFromJson(
  Map<String, dynamic> json,
) => CreateRosterProposalRequest(
  shiftId: json['shift_id'] as String,
  objective: json['objective'] as String?,
  allowCascadingSwap: json['allow_cascading_swap'] as bool?,
);

Map<String, dynamic> _$CreateRosterProposalRequestToJson(
  CreateRosterProposalRequest instance,
) => <String, dynamic>{
  'shift_id': instance.shiftId,
  'objective': instance.objective,
  'allow_cascading_swap': instance.allowCascadingSwap,
};
