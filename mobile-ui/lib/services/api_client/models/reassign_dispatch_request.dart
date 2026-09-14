// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'reassign_dispatch_request.g.dart';

@JsonSerializable()
class ReassignDispatchRequest {
  const ReassignDispatchRequest({
    this.replacementAmbulanceId,
    this.reason,
  });

  factory ReassignDispatchRequest.fromJson(Map<String, Object?> json) => _$ReassignDispatchRequestFromJson(json);

  @JsonKey(name: 'replacement_ambulance_id')
  final String? replacementAmbulanceId;
  final String? reason;

  Map<String, Object?> toJson() => _$ReassignDispatchRequestToJson(this);
}
