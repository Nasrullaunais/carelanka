// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

@JsonEnum()
enum RejectionReason {
  @JsonValue('unsafe_suggestion')
  unsafeSuggestion('unsafe_suggestion'),
  @JsonValue('source_ward_cannot_spare')
  sourceWardCannotSpare('source_ward_cannot_spare'),
  @JsonValue('staff_unsuitable')
  staffUnsuitable('staff_unsuitable'),
  @JsonValue('gap_filled_another_way')
  gapFilledAnotherWay('gap_filled_another_way'),
  @JsonValue('no_longer_needed')
  noLongerNeeded('no_longer_needed'),
  @JsonValue('other')
  other('other'),
  /// Default value for all unparsed values, allows backward compatibility when adding new values on the backend.
  $unknown(null);

  const RejectionReason(this.json);

  factory RejectionReason.fromJson(String json) => values.firstWhere(
        (e) => e.json == json,
        orElse: () => $unknown,
      );

  final String? json;
  String toJson() {
    final value = json;
    if (value == null) {
      throw StateError('Cannot convert enum value with null JSON representation to String. '
          'This usually happens for \$unknown or @JsonValue(null) entries.');
    }
    return value as String;
  }

  @override
  String toString() => json?.toString() ?? super.toString();
  /// Returns all defined enum values excluding the $unknown value.
  static List<RejectionReason> get $valuesDefined => values.where((value) => value != $unknown).toList();
}
