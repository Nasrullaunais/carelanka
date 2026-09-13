// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'admission_bed_paged_result.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AdmissionBedPagedResult _$AdmissionBedPagedResultFromJson(
  Map<String, dynamic> json,
) => AdmissionBedPagedResult(
  items: (json['items'] as List<dynamic>)
      .map((e) => AdmissionBed.fromJson(e as Map<String, dynamic>))
      .toList(),
  page: (json['page'] as num).toInt(),
  pageSize: (json['page_size'] as num).toInt(),
  totalItems: (json['total_items'] as num).toInt(),
  totalPages: (json['total_pages'] as num).toInt(),
);

Map<String, dynamic> _$AdmissionBedPagedResultToJson(
  AdmissionBedPagedResult instance,
) => <String, dynamic>{
  'items': instance.items,
  'page': instance.page,
  'page_size': instance.pageSize,
  'total_items': instance.totalItems,
  'total_pages': instance.totalPages,
};
