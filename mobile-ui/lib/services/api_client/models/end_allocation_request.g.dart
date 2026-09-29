// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'end_allocation_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

EndAllocationRequest _$EndAllocationRequestFromJson(
  Map<String, dynamic> json,
) => EndAllocationRequest(
  reason: AllocationEndReason.fromJson(json['reason'] as String),
  notes: json['notes'] as String?,
  suppressAgent: json['suppress_agent'] as bool?,
);

Map<String, dynamic> _$EndAllocationRequestToJson(
  EndAllocationRequest instance,
) => <String, dynamic>{
  'reason': instance.reason,
  'notes': instance.notes,
  'suppress_agent': instance.suppressAgent,
};
