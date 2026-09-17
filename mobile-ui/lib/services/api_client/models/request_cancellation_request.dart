// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'request_cancellation_request.g.dart';

@JsonSerializable()
class RequestCancellationRequest {
  const RequestCancellationRequest({
    this.reason,
  });

  factory RequestCancellationRequest.fromJson(Map<String, Object?> json) => _$RequestCancellationRequestFromJson(json);

  final String? reason;

  Map<String, Object?> toJson() => _$RequestCancellationRequestToJson(this);
}
