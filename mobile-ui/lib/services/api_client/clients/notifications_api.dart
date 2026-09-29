// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/inbox_notification.dart';
import '../models/inbox_notification_paged_result.dart';
import '../models/unread_notification_count.dart';

part 'notifications_api.g.dart';

@RestApi()
abstract class NotificationsApi {
  factory NotificationsApi(Dio dio, {String? baseUrl}) = _NotificationsApi;

  @GET('/notifications')
  Future<InboxNotificationPagedResult> listMyNotifications({
    @Query('unreadOnly') bool? unreadOnly,
    @Query('page') int? page,
    @Query('pageSize') int? pageSize,
  });

  @GET('/notifications/unread-count')
  Future<UnreadNotificationCount> getMyUnreadNotificationCount();

  @POST('/notifications/{id}/read')
  Future<InboxNotification> markNotificationRead({
    @Path('id') required String id,
  });

  @POST('/notifications/read-all')
  Future<UnreadNotificationCount> markAllNotificationsRead();
}
