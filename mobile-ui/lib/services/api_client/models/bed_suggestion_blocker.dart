// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'bed_suggestion_blocker_code.dart';

part 'bed_suggestion_blocker.g.dart';

@JsonSerializable()
class BedSuggestionBlocker {
  const BedSuggestionBlocker({
    required this.code,
    required this.message,
  });
  
  factory BedSuggestionBlocker.fromJson(Map<String, Object?> json) => _$BedSuggestionBlockerFromJson(json);
  
  final BedSuggestionBlockerCode code;
  final String? message;

  Map<String, Object?> toJson() => _$BedSuggestionBlockerToJson(this);
}
