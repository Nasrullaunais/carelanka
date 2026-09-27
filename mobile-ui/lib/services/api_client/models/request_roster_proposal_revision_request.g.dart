// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'request_roster_proposal_revision_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

RequestRosterProposalRevisionRequest
_$RequestRosterProposalRevisionRequestFromJson(Map<String, dynamic> json) =>
    RequestRosterProposalRevisionRequest(
      guidance: json['guidance'] as String,
      notes: json['notes'] as String?,
      excludeStaffIds: (json['exclude_staff_ids'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      excludeWardIds: (json['exclude_ward_ids'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
    );

Map<String, dynamic> _$RequestRosterProposalRevisionRequestToJson(
  RequestRosterProposalRevisionRequest instance,
) => <String, dynamic>{
  'guidance': instance.guidance,
  'notes': instance.notes,
  'exclude_staff_ids': instance.excludeStaffIds,
  'exclude_ward_ids': instance.excludeWardIds,
};
