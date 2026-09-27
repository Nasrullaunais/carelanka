// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'crew_candidate.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CrewCandidate _$CrewCandidateFromJson(Map<String, dynamic> json) =>
    CrewCandidate(
      staffMemberId: json['staff_member_id'] as String,
      fullName: json['full_name'] as String,
    );

Map<String, dynamic> _$CrewCandidateToJson(CrewCandidate instance) =>
    <String, dynamic>{
      'staff_member_id': instance.staffMemberId,
      'full_name': instance.fullName,
    };
