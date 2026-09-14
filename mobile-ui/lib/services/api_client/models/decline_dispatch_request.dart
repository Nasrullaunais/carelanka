// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'decline_dispatch_request.g.dart';

@JsonSerializable()
class DeclineDispatchRequest {
  const DeclineDispatchRequest({
    this.reason,
  });

  factory DeclineDispatchRequest.fromJson(Map<String, Object?> json) => _$DeclineDispatchRequestFromJson(json);

  final String? reason;

  Map<String, Object?> toJson() => _$DeclineDispatchRequestToJson(this);
}
