// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'staff_login_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

StaffLoginRequest _$StaffLoginRequestFromJson(Map<String, dynamic> json) =>
    StaffLoginRequest(
      email: json['email'] as String,
      password: json['password'] as String,
    );

Map<String, dynamic> _$StaffLoginRequestToJson(StaffLoginRequest instance) =>
    <String, dynamic>{'email': instance.email, 'password': instance.password};
