// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'update_reorder_threshold_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

UpdateReorderThresholdRequest _$UpdateReorderThresholdRequestFromJson(
  Map<String, dynamic> json,
) => UpdateReorderThresholdRequest(
  reorderThreshold: (json['reorder_threshold'] as num).toInt(),
);

Map<String, dynamic> _$UpdateReorderThresholdRequestToJson(
  UpdateReorderThresholdRequest instance,
) => <String, dynamic>{'reorder_threshold': instance.reorderThreshold};
