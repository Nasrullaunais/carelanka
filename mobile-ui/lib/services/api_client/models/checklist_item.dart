// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'checklist_item.g.dart';

@JsonSerializable()
class ChecklistItem {
  const ChecklistItem({
    required this.ticked,
    required this.mandatory,
    this.tickedByStaffId,
    this.tickedByStaffName,
    this.tickedAt,
  });
  
  factory ChecklistItem.fromJson(Map<String, Object?> json) => _$ChecklistItemFromJson(json);
  
  final bool ticked;
  @JsonKey(name: 'ticked_by_staff_id')
  final String? tickedByStaffId;
  @JsonKey(name: 'ticked_by_staff_name')
  final String? tickedByStaffName;
  @JsonKey(name: 'ticked_at')
  final DateTime? tickedAt;
  final bool mandatory;

  Map<String, Object?> toJson() => _$ChecklistItemToJson(this);
}
