// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'correct_bed_request.g.dart';

@JsonSerializable()
class CorrectBedRequest {
  const CorrectBedRequest({
    required this.bedId,
    this.reason,
  });
  
  factory CorrectBedRequest.fromJson(Map<String, Object?> json) => _$CorrectBedRequestFromJson(json);
  
  @JsonKey(name: 'bed_id')
  final String bedId;
  final String? reason;

  Map<String, Object?> toJson() => _$CorrectBedRequestToJson(this);
}
