// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

@JsonEnum()
enum PrincipalRole {
  @JsonValue('ward_nurse')
  wardNurse('ward_nurse'),
  @JsonValue('doctor')
  doctor('doctor'),
  @JsonValue('ambulance_crew')
  ambulanceCrew('ambulance_crew'),
  @JsonValue('general_staff')
  generalStaff('general_staff'),
  @JsonValue('duty_manager')
  dutyManager('duty_manager'),
  @JsonValue('hospital_administrator')
  hospitalAdministrator('hospital_administrator'),
  @JsonValue('equipment_manager')
  equipmentManager('equipment_manager'),
  @JsonValue('patient')
  patient('patient'),
  /// Default value for all unparsed values, allows backward compatibility when adding new values on the backend.
  $unknown(null);

  const PrincipalRole(this.json);

  factory PrincipalRole.fromJson(String json) => values.firstWhere(
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
  static List<PrincipalRole> get $valuesDefined => values.where((value) => value != $unknown).toList();
}
