// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'update_medical_profile_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

UpdateMedicalProfileRequest _$UpdateMedicalProfileRequestFromJson(
  Map<String, dynamic> json,
) => UpdateMedicalProfileRequest(
  knownConditions: json['known_conditions'] as String?,
  allergies: json['allergies'] as String?,
  currentSymptoms: json['current_symptoms'] as String?,
);

Map<String, dynamic> _$UpdateMedicalProfileRequestToJson(
  UpdateMedicalProfileRequest instance,
) => <String, dynamic>{
  'known_conditions': instance.knownConditions,
  'allergies': instance.allergies,
  'current_symptoms': instance.currentSymptoms,
};
