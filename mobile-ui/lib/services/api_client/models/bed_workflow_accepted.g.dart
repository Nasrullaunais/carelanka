// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'bed_workflow_accepted.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

BedWorkflowAccepted _$BedWorkflowAcceptedFromJson(Map<String, dynamic> json) =>
    BedWorkflowAccepted(
      workflowId: json['workflow_id'] as String?,
      admissionId: json['admission_id'] as String?,
      status: json['status'] == null
          ? null
          : BedWorkflowStatus.fromJson(json['status'] as String),
      pollUrl: json['poll_url'] as String?,
    );

Map<String, dynamic> _$BedWorkflowAcceptedToJson(
  BedWorkflowAccepted instance,
) => <String, dynamic>{
  'workflow_id': instance.workflowId,
  'admission_id': instance.admissionId,
  'status': instance.status,
  'poll_url': instance.pollUrl,
};
