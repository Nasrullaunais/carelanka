// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'pharmacy_category.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

PharmacyCategory _$PharmacyCategoryFromJson(Map<String, dynamic> json) =>
    PharmacyCategory(
      id: json['id'] as String,
      name: json['name'] as String,
      requiresPrescription: json['requires_prescription'] as bool,
      createdAt: DateTime.parse(json['created_at'] as String),
      updatedAt: DateTime.parse(json['updated_at'] as String),
    );

Map<String, dynamic> _$PharmacyCategoryToJson(PharmacyCategory instance) =>
    <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
      'requires_prescription': instance.requiresPrescription,
      'created_at': instance.createdAt.toIso8601String(),
      'updated_at': instance.updatedAt.toIso8601String(),
    };
