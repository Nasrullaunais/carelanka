// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'notification_type.dart';

part 'inbox_notification.g.dart';

@JsonSerializable()
class InboxNotification {
  const InboxNotification({
    this.id,
    this.type,
    this.title,
    this.body,
    this.entityType,
    this.entityId,
    this.readAt,
    this.createdAt,
  });
  
  factory InboxNotification.fromJson(Map<String, Object?> json) => _$InboxNotificationFromJson(json);
  
  final String? id;
  final NotificationType? type;
  final String? title;
  final String? body;
  @JsonKey(name: 'entity_type')
  final String? entityType;
  @JsonKey(name: 'entity_id')
  final String? entityId;
  @JsonKey(name: 'read_at')
  final DateTime? readAt;
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;

  Map<String, Object?> toJson() => _$InboxNotificationToJson(this);
}
