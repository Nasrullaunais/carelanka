// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'bed_suggestion_blocker.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

BedSuggestionBlocker _$BedSuggestionBlockerFromJson(
  Map<String, dynamic> json,
) => BedSuggestionBlocker(
  code: BedSuggestionBlockerCode.fromJson(json['code'] as String),
  message: json['message'] as String?,
);

Map<String, dynamic> _$BedSuggestionBlockerToJson(
  BedSuggestionBlocker instance,
) => <String, dynamic>{'code': instance.code, 'message': instance.message};
