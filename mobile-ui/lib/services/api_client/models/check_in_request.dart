// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'admission_category.dart';
import 'admission_urgency.dart';

part 'check_in_request.g.dart';

@JsonSerializable()
class CheckInRequest {
  const CheckInRequest({
    required this.admissionCategory,
    required this.urgency,
    this.isInfectious = false,
  });
  
  factory CheckInRequest.fromJson(Map<String, Object?> json) => _$CheckInRequestFromJson(json);
  
  @JsonKey(name: 'admission_category')
  final AdmissionCategory admissionCategory;
  final AdmissionUrgency urgency;
  @JsonKey(name: 'is_infectious')
  final bool isInfectious;

  Map<String, Object?> toJson() => _$CheckInRequestToJson(this);
}
