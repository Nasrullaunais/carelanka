// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'care_query_request.g.dart';

@JsonSerializable()
class CareQueryRequest {
  const CareQueryRequest({
    this.reportedText,
  });
  
  factory CareQueryRequest.fromJson(Map<String, Object?> json) => _$CareQueryRequestFromJson(json);
  
  @JsonKey(name: 'reported_text')
  final String? reportedText;

  Map<String, Object?> toJson() => _$CareQueryRequestToJson(this);
}
