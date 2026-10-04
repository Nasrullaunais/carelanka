// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

@JsonEnum()
enum EmergencyCallOutcome {
  @JsonValue('transported')
  transported('transported'),
  @JsonValue('treated_at_scene')
  treatedAtScene('treated_at_scene'),
  @JsonValue('refused_transport')
  refusedTransport('refused_transport'),
  @JsonValue('patient_not_found')
  patientNotFound('patient_not_found'),
  @JsonValue('deceased_at_scene')
  deceasedAtScene('deceased_at_scene'),
  @JsonValue('false_alarm')
  falseAlarm('false_alarm'),
  @JsonValue('duplicate_call')
  duplicateCall('duplicate_call'),
  @JsonValue('caller_cancelled')
  callerCancelled('caller_cancelled'),
  @JsonValue('no_longer_needed')
  noLongerNeeded('no_longer_needed'),
  /// Default value for all unparsed values, allows backward compatibility when adding new values on the backend.
  $unknown(null);

  const EmergencyCallOutcome(this.json);

  factory EmergencyCallOutcome.fromJson(String json) => values.firstWhere(
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
  static List<EmergencyCallOutcome> get $valuesDefined => values.where((value) => value != $unknown).toList();
}
