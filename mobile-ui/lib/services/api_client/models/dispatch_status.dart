// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

@JsonEnum()
enum DispatchStatus {
  @JsonValue('assigned')
  assigned('assigned'),
  @JsonValue('acknowledged')
  acknowledged('acknowledged'),
  @JsonValue('en_route_to_scene')
  enRouteToScene('en_route_to_scene'),
  @JsonValue('at_scene')
  atScene('at_scene'),
  @JsonValue('transporting_to_hospital')
  transportingToHospital('transporting_to_hospital'),
  @JsonValue('handed_over')
  handedOver('handed_over'),
  @JsonValue('declined')
  declined('declined'),
  @JsonValue('cancelled')
  cancelled('cancelled'),
  @JsonValue('reassigned')
  reassigned('reassigned'),
  /// Default value for all unparsed values, allows backward compatibility when adding new values on the backend.
  $unknown(null);

  const DispatchStatus(this.json);

  factory DispatchStatus.fromJson(String json) => values.firstWhere(
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
  static List<DispatchStatus> get $valuesDefined => values.where((value) => value != $unknown).toList();
}
