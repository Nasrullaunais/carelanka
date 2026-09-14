// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'create_pharmacy_category_request.g.dart';

@JsonSerializable()
class CreatePharmacyCategoryRequest {
  const CreatePharmacyCategoryRequest({
    required this.name,
    this.requiresPrescription,
  });
  
  factory CreatePharmacyCategoryRequest.fromJson(Map<String, Object?> json) => _$CreatePharmacyCategoryRequestFromJson(json);
  
  final String name;
  @JsonKey(name: 'requires_prescription')
  final bool? requiresPrescription;

  Map<String, Object?> toJson() => _$CreatePharmacyCategoryRequestToJson(this);
}
