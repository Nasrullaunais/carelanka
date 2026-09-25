// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'device_platform.dart';

part 'device_registration.g.dart';

@JsonSerializable()
class DeviceRegistration {
  const DeviceRegistration({
    this.id,
    this.platform,
    this.lastSeenAt,
  });
  
  factory DeviceRegistration.fromJson(Map<String, Object?> json) => _$DeviceRegistrationFromJson(json);
  
  final String? id;
  final DevicePlatform? platform;
  @JsonKey(name: 'last_seen_at')
  final DateTime? lastSeenAt;

  Map<String, Object?> toJson() => _$DeviceRegistrationToJson(this);
}
