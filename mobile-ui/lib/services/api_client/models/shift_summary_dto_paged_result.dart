// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'shift_summary_dto.dart';

part 'shift_summary_dto_paged_result.g.dart';

@JsonSerializable()
class ShiftSummaryDtoPagedResult {
  const ShiftSummaryDtoPagedResult({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalItems,
    required this.totalPages,
  });
  
  factory ShiftSummaryDtoPagedResult.fromJson(Map<String, Object?> json) => _$ShiftSummaryDtoPagedResultFromJson(json);
  
  final List<ShiftSummaryDto> items;
  final int page;
  @JsonKey(name: 'page_size')
  final int pageSize;
  @JsonKey(name: 'total_items')
  final int totalItems;
  @JsonKey(name: 'total_pages')
  final int totalPages;

  Map<String, Object?> toJson() => _$ShiftSummaryDtoPagedResultToJson(this);
}
