// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'cancel_dispatch_request.g.dart';

@JsonSerializable()
class CancelDispatchRequest {
  const CancelDispatchRequest({
    this.reason,
  });

  factory CancelDispatchRequest.fromJson(Map<String, Object?> json) => _$CancelDispatchRequestFromJson(json);

  final String? reason;

  Map<String, Object?> toJson() => _$CancelDispatchRequestToJson(this);
}
