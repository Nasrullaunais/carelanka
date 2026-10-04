// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'address_suggestion.g.dart';

@JsonSerializable()
class AddressSuggestion {
  const AddressSuggestion({
    this.label,
    this.latitude,
    this.longitude,
    this.approximateAccuracyMetres,
  });
  
  factory AddressSuggestion.fromJson(Map<String, Object?> json) => _$AddressSuggestionFromJson(json);
  
  final String? label;
  final double? latitude;
  final double? longitude;
  @JsonKey(name: 'approximate_accuracy_metres')
  final double? approximateAccuracyMetres;

  Map<String, Object?> toJson() => _$AddressSuggestionToJson(this);
}
