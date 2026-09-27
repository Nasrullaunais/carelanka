// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'admission_category.dart';

part 'classify_admission_request.g.dart';

@JsonSerializable()
class ClassifyAdmissionRequest {
  const ClassifyAdmissionRequest({
    this.admissionCategory,
  });
  
  factory ClassifyAdmissionRequest.fromJson(Map<String, Object?> json) => _$ClassifyAdmissionRequestFromJson(json);
  
  @JsonKey(name: 'admission_category')
  final AdmissionCategory? admissionCategory;

  Map<String, Object?> toJson() => _$ClassifyAdmissionRequestToJson(this);
}
