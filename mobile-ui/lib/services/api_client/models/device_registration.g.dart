// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'device_registration.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DeviceRegistration _$DeviceRegistrationFromJson(Map<String, dynamic> json) =>
    DeviceRegistration(
      id: json['id'] as String?,
      platform: json['platform'] == null
          ? null
          : DevicePlatform.fromJson(json['platform'] as String),
      lastSeenAt: json['last_seen_at'] == null
          ? null
          : DateTime.parse(json['last_seen_at'] as String),
    );

Map<String, dynamic> _$DeviceRegistrationToJson(DeviceRegistration instance) =>
    <String, dynamic>{
      'id': instance.id,
      'platform': instance.platform,
      'last_seen_at': instance.lastSeenAt?.toIso8601String(),
    };
