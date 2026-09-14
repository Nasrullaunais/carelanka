// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'update_ambulance_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

UpdateAmbulanceRequest _$UpdateAmbulanceRequestFromJson(
  Map<String, dynamic> json,
) => UpdateAmbulanceRequest(
  status: json['status'] == null
      ? null
      : AmbulanceStatus.fromJson(json['status'] as String),
  registrationNumber: json['registration_number'] as String?,
  outOfServiceReason: json['out_of_service_reason'] as String?,
);

Map<String, dynamic> _$UpdateAmbulanceRequestToJson(
  UpdateAmbulanceRequest instance,
) => <String, dynamic>{
  'status': instance.status,
  'registration_number': instance.registrationNumber,
  'out_of_service_reason': instance.outOfServiceReason,
};
