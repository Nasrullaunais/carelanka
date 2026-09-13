// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'staff_login_request.g.dart';

@JsonSerializable()
class StaffLoginRequest {
  const StaffLoginRequest({
    required this.email,
    required this.password,
  });
  
  factory StaffLoginRequest.fromJson(Map<String, Object?> json) => _$StaffLoginRequestFromJson(json);
  
  final String email;
  final String password;

  Map<String, Object?> toJson() => _$StaffLoginRequestToJson(this);
}
