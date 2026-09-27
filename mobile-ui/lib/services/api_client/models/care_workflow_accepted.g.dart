// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'care_workflow_accepted.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CareWorkflowAccepted _$CareWorkflowAcceptedFromJson(
  Map<String, dynamic> json,
) => CareWorkflowAccepted(
  workflowId: json['workflow_id'] as String?,
  recommendationId: json['recommendation_id'] as String?,
  status: json['status'] as String?,
  pollUrl: json['poll_url'] as String?,
  redFlag: json['red_flag'] as bool?,
);

Map<String, dynamic> _$CareWorkflowAcceptedToJson(
  CareWorkflowAccepted instance,
) => <String, dynamic>{
  'workflow_id': instance.workflowId,
  'recommendation_id': instance.recommendationId,
  'status': instance.status,
  'poll_url': instance.pollUrl,
  'red_flag': instance.redFlag,
};
