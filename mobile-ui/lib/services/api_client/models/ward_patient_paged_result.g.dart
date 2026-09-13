// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ward_patient_paged_result.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

WardPatientPagedResult _$WardPatientPagedResultFromJson(
  Map<String, dynamic> json,
) => WardPatientPagedResult(
  items: (json['items'] as List<dynamic>)
      .map((e) => WardPatient.fromJson(e as Map<String, dynamic>))
      .toList(),
  page: (json['page'] as num).toInt(),
  pageSize: (json['page_size'] as num).toInt(),
  totalItems: (json['total_items'] as num).toInt(),
  totalPages: (json['total_pages'] as num).toInt(),
);

Map<String, dynamic> _$WardPatientPagedResultToJson(
  WardPatientPagedResult instance,
) => <String, dynamic>{
  'items': instance.items,
  'page': instance.page,
  'page_size': instance.pageSize,
  'total_items': instance.totalItems,
  'total_pages': instance.totalPages,
};
