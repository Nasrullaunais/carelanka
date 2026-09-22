// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

@JsonEnum()
enum BedSuggestionBlockerCode {
  @JsonValue('downgrade_needed')
  downgradeNeeded('downgrade_needed'),
  @JsonValue('upgrade_only')
  upgradeOnly('upgrade_only'),
  @JsonValue('ward_full')
  wardFull('ward_full'),
  @JsonValue('gender_policy')
  genderPolicy('gender_policy'),
  @JsonValue('needs_isolation')
  needsIsolation('needs_isolation'),
  @JsonValue('pediatric_only')
  pediatricOnly('pediatric_only'),
  @JsonValue('no_bed_required')
  noBedRequired('no_bed_required'),
  @JsonValue('no_such_patient')
  noSuchPatient('no_such_patient'),
  @JsonValue('no_open_admission')
  noOpenAdmission('no_open_admission'),
  @JsonValue('agent_failed')
  agentFailed('agent_failed'),
  /// Default value for all unparsed values, allows backward compatibility when adding new values on the backend.
  $unknown(null);

  const BedSuggestionBlockerCode(this.json);

  factory BedSuggestionBlockerCode.fromJson(String json) => values.firstWhere(
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
  static List<BedSuggestionBlockerCode> get $valuesDefined => values.where((value) => value != $unknown).toList();
}
