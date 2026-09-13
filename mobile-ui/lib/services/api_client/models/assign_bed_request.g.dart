// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'assign_bed_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AssignBedRequest _$AssignBedRequestFromJson(Map<String, dynamic> json) =>
    AssignBedRequest(
      bedId: json['bed_id'] as String,
      overrideReason: json['override_reason'] as String?,
    );

Map<String, dynamic> _$AssignBedRequestToJson(AssignBedRequest instance) =>
    <String, dynamic>{
      'bed_id': instance.bedId,
      'override_reason': instance.overrideReason,
    };
