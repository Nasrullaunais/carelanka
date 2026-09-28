// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

@JsonEnum()
enum PersonTitle {
  @JsonValue('mr')
  mr('mr'),
  @JsonValue('mrs')
  mrs('mrs'),
  @JsonValue('ms')
  ms('ms'),
  @JsonValue('miss')
  miss('miss'),
  @JsonValue('dr')
  dr('dr'),
  @JsonValue('prof')
  prof('prof'),
  /// Default value for all unparsed values, allows backward compatibility when adding new values on the backend.
  $unknown(null);

  const PersonTitle(this.json);

  factory PersonTitle.fromJson(String json) => values.firstWhere(
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
  static List<PersonTitle> get $valuesDefined => values.where((value) => value != $unknown).toList();
}
