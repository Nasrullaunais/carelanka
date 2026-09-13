// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_pharmacy_category_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreatePharmacyCategoryRequest _$CreatePharmacyCategoryRequestFromJson(
  Map<String, dynamic> json,
) => CreatePharmacyCategoryRequest(
  name: json['name'] as String,
  requiresPrescription: json['requires_prescription'] as bool?,
);

Map<String, dynamic> _$CreatePharmacyCategoryRequestToJson(
  CreatePharmacyCategoryRequest instance,
) => <String, dynamic>{
  'name': instance.name,
  'requires_prescription': instance.requiresPrescription,
};
