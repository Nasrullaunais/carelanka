// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'unread_notification_count.g.dart';

@JsonSerializable()
class UnreadNotificationCount {
  const UnreadNotificationCount({
    this.count,
  });
  
  factory UnreadNotificationCount.fromJson(Map<String, Object?> json) => _$UnreadNotificationCountFromJson(json);
  
  final int? count;

  Map<String, Object?> toJson() => _$UnreadNotificationCountToJson(this);
}
