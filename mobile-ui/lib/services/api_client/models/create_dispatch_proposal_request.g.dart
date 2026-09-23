// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_dispatch_proposal_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreateDispatchProposalRequest _$CreateDispatchProposalRequestFromJson(
  Map<String, dynamic> json,
) => CreateDispatchProposalRequest(
  emergencyCallId: json['emergency_call_id'] as String?,
  allowDiversion: json['allow_diversion'] as bool?,
  excludeAmbulanceIds: (json['exclude_ambulance_ids'] as List<dynamic>?)
      ?.map((e) => e as String)
      .toList(),
);

Map<String, dynamic> _$CreateDispatchProposalRequestToJson(
  CreateDispatchProposalRequest instance,
) => <String, dynamic>{
  'emergency_call_id': instance.emergencyCallId,
  'allow_diversion': instance.allowDiversion,
  'exclude_ambulance_ids': instance.excludeAmbulanceIds,
};
