// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'review_cancellation_request.g.dart';

@JsonSerializable()
class ReviewCancellationRequest {
  const ReviewCancellationRequest({
    this.notes,
  });

  factory ReviewCancellationRequest.fromJson(Map<String, Object?> json) => _$ReviewCancellationRequestFromJson(json);

  final String? notes;

  Map<String, Object?> toJson() => _$ReviewCancellationRequestToJson(this);
}
