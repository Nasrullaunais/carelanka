// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'staff_role.dart';

part 'staff_lookup_result.g.dart';

@JsonSerializable()
class StaffLookupResult {
  const StaffLookupResult({
    required this.staffId,
    required this.found,
    this.fullName,
    this.role,
    this.isActive,
  });
  
  factory StaffLookupResult.fromJson(Map<String, Object?> json) => _$StaffLookupResultFromJson(json);
  
  @JsonKey(name: 'staff_id')
  final String staffId;
  final bool found;
  @JsonKey(name: 'full_name')
  final String? fullName;
  final StaffRole? role;
  @JsonKey(name: 'is_active')
  final bool? isActive;

  Map<String, Object?> toJson() => _$StaffLookupResultToJson(this);
}
