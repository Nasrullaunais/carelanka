// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'allocation_end_reason.dart';

part 'end_allocation_request.g.dart';

@JsonSerializable()
class EndAllocationRequest {
  const EndAllocationRequest({
    required this.reason,
    this.notes,
    this.suppressAgent,
  });
  
  factory EndAllocationRequest.fromJson(Map<String, Object?> json) => _$EndAllocationRequestFromJson(json);
  
  final AllocationEndReason reason;
  final String? notes;
  @JsonKey(name: 'suppress_agent')
  final bool? suppressAgent;

  Map<String, Object?> toJson() => _$EndAllocationRequestToJson(this);
}
