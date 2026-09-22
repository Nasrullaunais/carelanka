// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'dispatch_summary.dart';

part 'dispatch_summary_paged_result.g.dart';

@JsonSerializable()
class DispatchSummaryPagedResult {
  const DispatchSummaryPagedResult({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalItems,
    required this.totalPages,
  });
  
  factory DispatchSummaryPagedResult.fromJson(Map<String, Object?> json) => _$DispatchSummaryPagedResultFromJson(json);
  
  final List<DispatchSummary> items;
  final int page;
  @JsonKey(name: 'page_size')
  final int pageSize;
  @JsonKey(name: 'total_items')
  final int totalItems;
  @JsonKey(name: 'total_pages')
  final int totalPages;

  Map<String, Object?> toJson() => _$DispatchSummaryPagedResultToJson(this);
}
