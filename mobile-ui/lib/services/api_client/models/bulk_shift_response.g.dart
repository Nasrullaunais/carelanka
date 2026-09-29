// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'bulk_shift_response.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

BulkShiftResponse _$BulkShiftResponseFromJson(Map<String, dynamic> json) =>
    BulkShiftResponse(
      created: (json['created'] as num).toInt(),
      skipped: (json['skipped'] as num).toInt(),
      shifts: (json['shifts'] as List<dynamic>)
          .map((e) => ShiftSummaryDto.fromJson(e as Map<String, dynamic>))
          .toList(),
    );

Map<String, dynamic> _$BulkShiftResponseToJson(BulkShiftResponse instance) =>
    <String, dynamic>{
      'created': instance.created,
      'skipped': instance.skipped,
      'shifts': instance.shifts,
    };
