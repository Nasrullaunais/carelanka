// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ambulance_handover.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AmbulanceHandover _$AmbulanceHandoverFromJson(Map<String, dynamic> json) =>
    AmbulanceHandover(
      ambulanceRegistration: json['ambulance_registration'] as String?,
      handedOverAt: json['handed_over_at'] == null
          ? null
          : DateTime.parse(json['handed_over_at'] as String),
      patientCondition: json['patient_condition'] as String?,
      notes: json['notes'] as String?,
    );

Map<String, dynamic> _$AmbulanceHandoverToJson(AmbulanceHandover instance) =>
    <String, dynamic>{
      'ambulance_registration': instance.ambulanceRegistration,
      'handed_over_at': instance.handedOverAt?.toIso8601String(),
      'patient_condition': instance.patientCondition,
      'notes': instance.notes,
    };
