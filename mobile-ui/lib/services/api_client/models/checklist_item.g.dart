// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'checklist_item.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ChecklistItem _$ChecklistItemFromJson(Map<String, dynamic> json) =>
    ChecklistItem(
      ticked: json['ticked'] as bool,
      mandatory: json['mandatory'] as bool,
      tickedByStaffId: json['ticked_by_staff_id'] as String?,
      tickedByStaffName: json['ticked_by_staff_name'] as String?,
      tickedAt: json['ticked_at'] == null
          ? null
          : DateTime.parse(json['ticked_at'] as String),
    );

Map<String, dynamic> _$ChecklistItemToJson(ChecklistItem instance) =>
    <String, dynamic>{
      'ticked': instance.ticked,
      'ticked_by_staff_id': instance.tickedByStaffId,
      'ticked_by_staff_name': instance.tickedByStaffName,
      'ticked_at': instance.tickedAt?.toIso8601String(),
      'mandatory': instance.mandatory,
    };
