// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'manual_dispatch_request.g.dart';

@JsonSerializable()
class ManualDispatchRequest {
  const ManualDispatchRequest({
    this.ambulanceId,
  });

  factory ManualDispatchRequest.fromJson(Map<String, Object?> json) => _$ManualDispatchRequestFromJson(json);

  @JsonKey(name: 'ambulance_id')
  final String? ambulanceId;

  Map<String, Object?> toJson() => _$ManualDispatchRequestToJson(this);
}
