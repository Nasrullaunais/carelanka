// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

@JsonEnum()
enum DispatchRejectionReason {
  @JsonValue('unsafe_diversion')
  unsafeDiversion('unsafe_diversion'),
  @JsonValue('source_call_too_urgent_to_divert')
  sourceCallTooUrgentToDivert('source_call_too_urgent_to_divert'),
  @JsonValue('ambulance_unsuitable')
  ambulanceUnsuitable('ambulance_unsuitable'),
  @JsonValue('handled_another_way')
  handledAnotherWay('handled_another_way'),
  @JsonValue('no_longer_needed')
  noLongerNeeded('no_longer_needed'),
  @JsonValue('other')
  other('other'),
  /// Default value for all unparsed values, allows backward compatibility when adding new values on the backend.
  $unknown(null);

  const DispatchRejectionReason(this.json);

  factory DispatchRejectionReason.fromJson(String json) => values.firstWhere(
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
  static List<DispatchRejectionReason> get $valuesDefined => values.where((value) => value != $unknown).toList();
}
