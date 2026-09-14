// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'correct_bed_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CorrectBedRequest _$CorrectBedRequestFromJson(Map<String, dynamic> json) =>
    CorrectBedRequest(
      bedId: json['bed_id'] as String,
      reason: json['reason'] as String?,
    );

Map<String, dynamic> _$CorrectBedRequestToJson(CorrectBedRequest instance) =>
    <String, dynamic>{'bed_id': instance.bedId, 'reason': instance.reason};
