// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'bed_workflow_summary.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

BedWorkflowSummary _$BedWorkflowSummaryFromJson(
  Map<String, dynamic> json,
) => BedWorkflowSummary(
  workflowId: json['workflow_id'] as String?,
  admissionId: json['admission_id'] as String?,
  objective: json['objective'] as String?,
  status: json['status'] == null
      ? null
      : BedWorkflowStatus.fromJson(json['status'] as String),
  outcome: json['outcome'] == null
      ? null
      : BedAgentOutcome.fromJson(json['outcome'] as String),
  plan: (json['plan'] as List<dynamic>?)?.map((e) => e as String).toList(),
  steps: (json['steps'] as List<dynamic>?)
      ?.map((e) => BedAgentStep.fromJson(e as Map<String, dynamic>))
      .toList(),
  patient: json['patient'] == null
      ? null
      : BedSuggestionPatient.fromJson(json['patient'] as Map<String, dynamic>),
  best: json['best'] == null
      ? null
      : SuggestedBed.fromJson(json['best'] as Map<String, dynamic>),
  alternatives: (json['alternatives'] as List<dynamic>?)
      ?.map((e) => SuggestedBed.fromJson(e as Map<String, dynamic>))
      .toList(),
  blocker: json['blocker'] == null
      ? null
      : BedSuggestionBlocker.fromJson(json['blocker'] as Map<String, dynamic>),
  validation: json['validation'] == null
      ? null
      : BedWorkflowValidation.fromJson(
          json['validation'] as Map<String, dynamic>,
        ),
  requiresApprovalBy: json['requires_approval_by'] == null
      ? null
      : BedApproverRole.fromJson(json['requires_approval_by'] as String),
  retries: (json['retries'] as num?)?.toInt(),
);

Map<String, dynamic> _$BedWorkflowSummaryToJson(BedWorkflowSummary instance) =>
    <String, dynamic>{
      'workflow_id': instance.workflowId,
      'admission_id': instance.admissionId,
      'objective': instance.objective,
      'status': instance.status,
      'outcome': instance.outcome,
      'plan': instance.plan,
      'steps': instance.steps,
      'patient': instance.patient,
      'best': instance.best,
      'alternatives': instance.alternatives,
      'blocker': instance.blocker,
      'validation': instance.validation,
      'requires_approval_by': instance.requiresApprovalBy,
      'retries': instance.retries,
    };
