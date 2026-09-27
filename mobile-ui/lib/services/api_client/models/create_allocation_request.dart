// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'create_allocation_request.g.dart';

@JsonSerializable()
class CreateAllocationRequest {
  const CreateAllocationRequest({
    required this.shiftId,
    required this.staffMemberId,
    this.override,
    this.overrideReason,
  });
  
  factory CreateAllocationRequest.fromJson(Map<String, Object?> json) => _$CreateAllocationRequestFromJson(json);
  
  @JsonKey(name: 'shift_id')
  final String shiftId;
  @JsonKey(name: 'staff_member_id')
  final String staffMemberId;
  final bool? override;
  @JsonKey(name: 'override_reason')
  final String? overrideReason;

  Map<String, Object?> toJson() => _$CreateAllocationRequestToJson(this);
}
