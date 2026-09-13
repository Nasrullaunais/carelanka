// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'principal_role.dart';
import 'principal_type.dart';

part 'current_principal.g.dart';

@JsonSerializable()
class CurrentPrincipal {
  const CurrentPrincipal({
    required this.id,
    required this.principalType,
    required this.role,
    required this.displayName,
    this.email,
    this.phoneNumber,
    this.patientId,
  });
  
  factory CurrentPrincipal.fromJson(Map<String, Object?> json) => _$CurrentPrincipalFromJson(json);
  
  final String id;
  @JsonKey(name: 'principal_type')
  final PrincipalType principalType;
  final PrincipalRole role;
  @JsonKey(name: 'display_name')
  final String displayName;
  final String? email;
  @JsonKey(name: 'phone_number')
  final String? phoneNumber;
  @JsonKey(name: 'patient_id')
  final String? patientId;

  Map<String, Object?> toJson() => _$CurrentPrincipalToJson(this);
}
