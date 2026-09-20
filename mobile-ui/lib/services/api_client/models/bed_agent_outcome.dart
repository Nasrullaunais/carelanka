// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

@JsonEnum()
enum BedAgentOutcome {
  @JsonValue('proposed')
  proposed('proposed'),
  @JsonValue('proposed_with_downgrade')
  proposedWithDowngrade('proposed_with_downgrade'),
  @JsonValue('needs_duty_manager')
  needsDutyManager('needs_duty_manager'),
  @JsonValue('no_bed_available')
  noBedAvailable('no_bed_available'),
  @JsonValue('visit_needs_no_bed')
  visitNeedsNoBed('visit_needs_no_bed'),
  @JsonValue('patient_not_found')
  patientNotFound('patient_not_found'),
  @JsonValue('failed')
  failed('failed'),
  /// Default value for all unparsed values, allows backward compatibility when adding new values on the backend.
  $unknown(null);

  const BedAgentOutcome(this.json);

  factory BedAgentOutcome.fromJson(String json) => values.firstWhere(
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
  static List<BedAgentOutcome> get $valuesDefined => values.where((value) => value != $unknown).toList();
}
