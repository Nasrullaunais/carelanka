// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'update_my_dispatch_status_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

UpdateMyDispatchStatusRequest _$UpdateMyDispatchStatusRequestFromJson(
  Map<String, dynamic> json,
) => UpdateMyDispatchStatusRequest(
  status: json['status'] == null
      ? null
      : DispatchStatus.fromJson(json['status'] as String),
  latitude: (json['latitude'] as num?)?.toDouble(),
  longitude: (json['longitude'] as num?)?.toDouble(),
);

Map<String, dynamic> _$UpdateMyDispatchStatusRequestToJson(
  UpdateMyDispatchStatusRequest instance,
) => <String, dynamic>{
  'status': instance.status,
  'latitude': instance.latitude,
  'longitude': instance.longitude,
};
