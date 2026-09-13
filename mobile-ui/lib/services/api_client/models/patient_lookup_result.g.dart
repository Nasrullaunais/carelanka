// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'patient_lookup_result.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

PatientLookupResult _$PatientLookupResultFromJson(Map<String, dynamic> json) =>
    PatientLookupResult(
      found: json['found'] as bool,
      hasOpenAdmission: json['has_open_admission'] as bool,
      patient: json['patient'] == null
          ? null
          : PatientSummary.fromJson(json['patient'] as Map<String, dynamic>),
    );

Map<String, dynamic> _$PatientLookupResultToJson(
  PatientLookupResult instance,
) => <String, dynamic>{
  'found': instance.found,
  'patient': instance.patient,
  'has_open_admission': instance.hasOpenAdmission,
};
