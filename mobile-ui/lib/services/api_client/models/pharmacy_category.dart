// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'pharmacy_category.g.dart';

@JsonSerializable()
class PharmacyCategory {
  const PharmacyCategory({
    required this.id,
    required this.name,
    required this.requiresPrescription,
    required this.createdAt,
    required this.updatedAt,
  });
  
  factory PharmacyCategory.fromJson(Map<String, Object?> json) => _$PharmacyCategoryFromJson(json);
  
  final String id;
  final String name;
  @JsonKey(name: 'requires_prescription')
  final bool requiresPrescription;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  Map<String, Object?> toJson() => _$PharmacyCategoryToJson(this);
}
