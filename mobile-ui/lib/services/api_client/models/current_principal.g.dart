// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'current_principal.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CurrentPrincipal _$CurrentPrincipalFromJson(Map<String, dynamic> json) =>
    CurrentPrincipal(
      id: json['id'] as String,
      principalType: PrincipalType.fromJson(json['principal_type'] as String),
      role: PrincipalRole.fromJson(json['role'] as String),
      displayName: json['display_name'] as String,
      email: json['email'] as String?,
      phoneNumber: json['phone_number'] as String?,
      patientId: json['patient_id'] as String?,
    );

Map<String, dynamic> _$CurrentPrincipalToJson(CurrentPrincipal instance) =>
    <String, dynamic>{
      'id': instance.id,
      'principal_type': instance.principalType,
      'role': instance.role,
      'display_name': instance.displayName,
      'email': instance.email,
      'phone_number': instance.phoneNumber,
      'patient_id': instance.patientId,
    };
