// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'reorder_workflow_summary.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ReorderWorkflowSummary _$ReorderWorkflowSummaryFromJson(
  Map<String, dynamic> json,
) => ReorderWorkflowSummary(
  workflowId: json['workflow_id'] as String?,
  suggestionId: json['suggestion_id'] as String?,
  pharmacyItemId: json['pharmacy_item_id'] as String?,
  objective: json['objective'] as String?,
  status: json['status'] == null
      ? null
      : ReorderWorkflowStatus.fromJson(json['status'] as String),
  plan: (json['plan'] as List<dynamic>?)?.map((e) => e as String).toList(),
  steps: (json['steps'] as List<dynamic>?)
      ?.map((e) => CareAgentStep.fromJson(e as Map<String, dynamic>))
      .toList(),
  currentThreshold: (json['current_threshold'] as num?)?.toInt(),
  currentQuantityOnHand: (json['current_quantity_on_hand'] as num?)?.toInt(),
  suggestedThreshold: (json['suggested_threshold'] as num?)?.toInt(),
  reasoning: json['reasoning'] as String?,
  source: json['source'] == null
      ? null
      : ReorderSuggestionSource.fromJson(json['source'] as String),
  retries: (json['retries'] as num?)?.toInt(),
);

Map<String, dynamic> _$ReorderWorkflowSummaryToJson(
  ReorderWorkflowSummary instance,
) => <String, dynamic>{
  'workflow_id': instance.workflowId,
  'suggestion_id': instance.suggestionId,
  'pharmacy_item_id': instance.pharmacyItemId,
  'objective': instance.objective,
  'status': instance.status,
  'plan': instance.plan,
  'steps': instance.steps,
  'current_threshold': instance.currentThreshold,
  'current_quantity_on_hand': instance.currentQuantityOnHand,
  'suggested_threshold': instance.suggestedThreshold,
  'reasoning': instance.reasoning,
  'source': instance.source,
  'retries': instance.retries,
};
