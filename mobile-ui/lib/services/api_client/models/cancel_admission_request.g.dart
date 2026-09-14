// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'cancel_admission_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CancelAdmissionRequest _$CancelAdmissionRequestFromJson(
  Map<String, dynamic> json,
) => CancelAdmissionRequest(
  reason: CancelReason.fromJson(json['reason'] as String),
  note: json['note'] as String?,
);

Map<String, dynamic> _$CancelAdmissionRequestToJson(
  CancelAdmissionRequest instance,
) => <String, dynamic>{'reason': instance.reason, 'note': instance.note};
