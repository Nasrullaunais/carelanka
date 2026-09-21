// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'care_workflow_summary.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CareWorkflowSummary _$CareWorkflowSummaryFromJson(Map<String, dynamic> json) =>
    CareWorkflowSummary(
      workflowId: json['workflow_id'] as String?,
      recommendationId: json['recommendation_id'] as String?,
      objective: json['objective'] as String?,
      status: json['status'] == null
          ? null
          : CareWorkflowStatus.fromJson(json['status'] as String),
      outcome: json['outcome'] == null
          ? null
          : CareAgentOutcome.fromJson(json['outcome'] as String),
      plan: (json['plan'] as List<dynamic>?)?.map((e) => e as String).toList(),
      steps: (json['steps'] as List<dynamic>?)
          ?.map((e) => BedAgentStep.fromJson(e as Map<String, dynamic>))
          .toList(),
      redFlag: json['red_flag'] as bool?,
      validation: json['validation'] == null
          ? null
          : CareWorkflowValidation.fromJson(
              json['validation'] as Map<String, dynamic>,
            ),
      draftSource: json['draft_source'] == null
          ? null
          : CareDraftSource.fromJson(json['draft_source'] as String),
      draftNote: json['draft_note'] as String?,
      retries: (json['retries'] as num?)?.toInt(),
    );

Map<String, dynamic> _$CareWorkflowSummaryToJson(
  CareWorkflowSummary instance,
) => <String, dynamic>{
  'workflow_id': instance.workflowId,
  'recommendation_id': instance.recommendationId,
  'objective': instance.objective,
  'status': instance.status,
  'outcome': instance.outcome,
  'plan': instance.plan,
  'steps': instance.steps,
  'red_flag': instance.redFlag,
  'validation': instance.validation,
  'draft_source': instance.draftSource,
  'draft_note': instance.draftNote,
  'retries': instance.retries,
};
