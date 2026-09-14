// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'reassign_dispatch_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ReassignDispatchRequest _$ReassignDispatchRequestFromJson(
  Map<String, dynamic> json,
) => ReassignDispatchRequest(
  replacementAmbulanceId: json['replacement_ambulance_id'] as String?,
  reason: json['reason'] as String?,
);

Map<String, dynamic> _$ReassignDispatchRequestToJson(
  ReassignDispatchRequest instance,
) => <String, dynamic>{
  'replacement_ambulance_id': instance.replacementAmbulanceId,
  'reason': instance.reason,
};
