// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

@JsonEnum()
enum CancelReason {
  @JsonValue('diverted_to_other_hospital')
  divertedToOtherHospital('diverted_to_other_hospital'),
  @JsonValue('false_alarm')
  falseAlarm('false_alarm'),
  @JsonValue('died_en_route')
  diedEnRoute('died_en_route'),
  @JsonValue('patient_refused')
  patientRefused('patient_refused'),
  @JsonValue('no_show')
  noShow('no_show'),
  /// Default value for all unparsed values, allows backward compatibility when adding new values on the backend.
  $unknown(null);

  const CancelReason(this.json);

  factory CancelReason.fromJson(String json) => values.firstWhere(
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
  static List<CancelReason> get $valuesDefined => values.where((value) => value != $unknown).toList();
}
