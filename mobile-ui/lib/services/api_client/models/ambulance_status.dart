// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

@JsonEnum()
enum AmbulanceStatus {
  @JsonValue('available')
  available('available'),
  @JsonValue('dispatched')
  dispatched('dispatched'),
  @JsonValue('en_route')
  enRoute('en_route'),
  @JsonValue('at_scene')
  atScene('at_scene'),
  @JsonValue('transporting')
  transporting('transporting'),
  @JsonValue('out_of_service')
  outOfService('out_of_service'),
  /// Default value for all unparsed values, allows backward compatibility when adding new values on the backend.
  $unknown(null);

  const AmbulanceStatus(this.json);

  factory AmbulanceStatus.fromJson(String json) => values.firstWhere(
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
  static List<AmbulanceStatus> get $valuesDefined => values.where((value) => value != $unknown).toList();
}
