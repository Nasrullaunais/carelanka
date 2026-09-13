// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'checklist_update_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ChecklistUpdateRequest _$ChecklistUpdateRequestFromJson(
  Map<String, dynamic> json,
) => ChecklistUpdateRequest(
  clinicalClearance: json['clinical_clearance'] as bool?,
  billingSettled: json['billing_settled'] as bool?,
);

Map<String, dynamic> _$ChecklistUpdateRequestToJson(
  ChecklistUpdateRequest instance,
) => <String, dynamic>{
  'clinical_clearance': instance.clinicalClearance,
  'billing_settled': instance.billingSettled,
};
