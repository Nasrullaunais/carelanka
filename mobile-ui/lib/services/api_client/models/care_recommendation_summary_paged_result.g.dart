// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'care_recommendation_summary_paged_result.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CareRecommendationSummaryPagedResult
_$CareRecommendationSummaryPagedResultFromJson(Map<String, dynamic> json) =>
    CareRecommendationSummaryPagedResult(
      items: (json['items'] as List<dynamic>)
          .map(
            (e) =>
                CareRecommendationSummary.fromJson(e as Map<String, dynamic>),
          )
          .toList(),
      page: (json['page'] as num).toInt(),
      pageSize: (json['page_size'] as num).toInt(),
      totalItems: (json['total_items'] as num).toInt(),
      totalPages: (json['total_pages'] as num).toInt(),
    );

Map<String, dynamic> _$CareRecommendationSummaryPagedResultToJson(
  CareRecommendationSummaryPagedResult instance,
) => <String, dynamic>{
  'items': instance.items,
  'page': instance.page,
  'page_size': instance.pageSize,
  'total_items': instance.totalItems,
  'total_pages': instance.totalPages,
};
