// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'reject_prescription_request.g.dart';

@JsonSerializable()
class RejectPrescriptionRequest {
  const RejectPrescriptionRequest({
    required this.reason,
  });
  
  factory RejectPrescriptionRequest.fromJson(Map<String, Object?> json) => _$RejectPrescriptionRequestFromJson(json);
  
  final String? reason;

  Map<String, Object?> toJson() => _$RejectPrescriptionRequestToJson(this);
}
