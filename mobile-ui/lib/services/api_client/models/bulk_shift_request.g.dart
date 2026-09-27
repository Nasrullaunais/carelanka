// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'bulk_shift_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

BulkShiftRequest _$BulkShiftRequestFromJson(Map<String, dynamic> json) =>
    BulkShiftRequest(
      wardId: json['ward_id'] as String,
      from: DateTime.parse(json['from'] as String),
      to: DateTime.parse(json['to'] as String),
      patterns: (json['patterns'] as List<dynamic>)
          .map((e) => BulkShiftPatternItem.fromJson(e as Map<String, dynamic>))
          .toList(),
      weekdays: (json['weekdays'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
    );

Map<String, dynamic> _$BulkShiftRequestToJson(BulkShiftRequest instance) =>
    <String, dynamic>{
      'ward_id': instance.wardId,
      'from': instance.from.toIso8601String(),
      'to': instance.to.toIso8601String(),
      'weekdays': instance.weekdays,
      'patterns': instance.patterns,
    };
