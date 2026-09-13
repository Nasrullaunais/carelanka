// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'cancel_reason.dart';

part 'cancel_admission_request.g.dart';

@JsonSerializable()
class CancelAdmissionRequest {
  const CancelAdmissionRequest({
    required this.reason,
    this.note,
  });
  
  factory CancelAdmissionRequest.fromJson(Map<String, Object?> json) => _$CancelAdmissionRequestFromJson(json);
  
  final CancelReason reason;
  final String? note;

  Map<String, Object?> toJson() => _$CancelAdmissionRequestToJson(this);
}
