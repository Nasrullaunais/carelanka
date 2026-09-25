// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'reorder_suggestion_accepted.g.dart';

@JsonSerializable()
class ReorderSuggestionAccepted {
  const ReorderSuggestionAccepted({
    this.workflowId,
    this.suggestionId,
    this.status,
    this.pollUrl,
  });
  
  factory ReorderSuggestionAccepted.fromJson(Map<String, Object?> json) => _$ReorderSuggestionAcceptedFromJson(json);
  
  @JsonKey(name: 'workflow_id')
  final String? workflowId;
  @JsonKey(name: 'suggestion_id')
  final String? suggestionId;
  final String? status;
  @JsonKey(name: 'poll_url')
  final String? pollUrl;

  Map<String, Object?> toJson() => _$ReorderSuggestionAcceptedToJson(this);
}
