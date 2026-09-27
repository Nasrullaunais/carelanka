// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'reorder_suggestion_accepted.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ReorderSuggestionAccepted _$ReorderSuggestionAcceptedFromJson(
  Map<String, dynamic> json,
) => ReorderSuggestionAccepted(
  workflowId: json['workflow_id'] as String?,
  suggestionId: json['suggestion_id'] as String?,
  status: json['status'] as String?,
  pollUrl: json['poll_url'] as String?,
);

Map<String, dynamic> _$ReorderSuggestionAcceptedToJson(
  ReorderSuggestionAccepted instance,
) => <String, dynamic>{
  'workflow_id': instance.workflowId,
  'suggestion_id': instance.suggestionId,
  'status': instance.status,
  'poll_url': instance.pollUrl,
};
