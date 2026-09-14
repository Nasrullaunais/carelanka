// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_ambulance_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreateAmbulanceRequest _$CreateAmbulanceRequestFromJson(
  Map<String, dynamic> json,
) => CreateAmbulanceRequest(
  registrationNumber: json['registration_number'] as String?,
  currentLatitude: (json['current_latitude'] as num?)?.toDouble(),
  currentLongitude: (json['current_longitude'] as num?)?.toDouble(),
);

Map<String, dynamic> _$CreateAmbulanceRequestToJson(
  CreateAmbulanceRequest instance,
) => <String, dynamic>{
  'registration_number': instance.registrationNumber,
  'current_latitude': instance.currentLatitude,
  'current_longitude': instance.currentLongitude,
};
