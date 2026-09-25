// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'lookup_staff_request.g.dart';

@JsonSerializable()
class LookupStaffRequest {
  const LookupStaffRequest({
    required this.staffIds,
  });
  
  factory LookupStaffRequest.fromJson(Map<String, Object?> json) => _$LookupStaffRequestFromJson(json);
  
  @JsonKey(name: 'staff_ids')
  final List<String> staffIds;

  Map<String, Object?> toJson() => _$LookupStaffRequestToJson(this);
}
