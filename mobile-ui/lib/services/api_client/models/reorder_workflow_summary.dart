// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'care_agent_step.dart';
import 'reorder_suggestion_source.dart';
import 'reorder_workflow_status.dart';

part 'reorder_workflow_summary.g.dart';

@JsonSerializable()
class ReorderWorkflowSummary {
  const ReorderWorkflowSummary({
    this.workflowId,
    this.suggestionId,
    this.pharmacyItemId,
    this.objective,
    this.status,
    this.plan,
    this.steps,
    this.currentThreshold,
    this.currentQuantityOnHand,
    this.suggestedThreshold,
    this.reasoning,
    this.source,
    this.retries,
  });
  
  factory ReorderWorkflowSummary.fromJson(Map<String, Object?> json) => _$ReorderWorkflowSummaryFromJson(json);
  
  @JsonKey(name: 'workflow_id')
  final String? workflowId;
  @JsonKey(name: 'suggestion_id')
  final String? suggestionId;
  @JsonKey(name: 'pharmacy_item_id')
  final String? pharmacyItemId;
  final String? objective;
  final ReorderWorkflowStatus? status;
  final List<String>? plan;
  final List<CareAgentStep>? steps;
  @JsonKey(name: 'current_threshold')
  final int? currentThreshold;
  @JsonKey(name: 'current_quantity_on_hand')
  final int? currentQuantityOnHand;
  @JsonKey(name: 'suggested_threshold')
  final int? suggestedThreshold;
  final String? reasoning;
  final ReorderSuggestionSource? source;
  final int? retries;

  Map<String, Object?> toJson() => _$ReorderWorkflowSummaryToJson(this);
}
