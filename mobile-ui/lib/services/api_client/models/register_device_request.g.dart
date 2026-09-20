// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'register_device_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

RegisterDeviceRequest _$RegisterDeviceRequestFromJson(
  Map<String, dynamic> json,
) => RegisterDeviceRequest(
  token: json['token'] as String?,
  platform: json['platform'] == null
      ? null
      : DevicePlatform.fromJson(json['platform'] as String),
);

Map<String, dynamic> _$RegisterDeviceRequestToJson(
  RegisterDeviceRequest instance,
) => <String, dynamic>{'token': instance.token, 'platform': instance.platform};
