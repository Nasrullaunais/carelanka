import 'package:intl/intl.dart';

// Renders local time everywhere — the API sends DateTimeOffset in UTC, so .toLocal() is not optional.
class FriendlyDate {
  const FriendlyDate._();

  static String date(DateTime value) => DateFormat('dd/MM/yyyy').format(value.toLocal());

  static String dayAndMonth(DateTime value) => DateFormat('EEE, d MMM').format(value.toLocal());

  static String time(DateTime value) => DateFormat('h:mm a').format(value.toLocal());

  static String full(DateTime value) =>
      '${DateFormat('EEE, d MMM yyyy').format(value.toLocal())} at ${time(value)}';

  static String relativeDay(DateTime value, {DateTime? now}) {
    final days = _calendarDaysFromNow(value, now);
    return switch (days) {
      0 => 'Today',
      1 => 'Tomorrow',
      -1 => 'Yesterday',
      _ => dayAndMonth(value),
    };
  }

  static String relativeDayAndTime(DateTime value, {DateTime? now}) =>
      '${relativeDay(value, now: now)} at ${time(value)}';

  static String countdown(DateTime value, {DateTime? now}) {
    final current = now ?? DateTime.now();
    final target = value.toLocal();
    final difference = target.difference(current);
    final ahead = !difference.isNegative;
    final gap = difference.abs();

    if (gap.inMinutes < 1) return 'Now';

    final amount = switch (gap) {
      _ when gap.inDays >= 365 => _plural(gap.inDays ~/ 365, 'year'),
      _ when gap.inDays >= 30 => _plural(gap.inDays ~/ 30, 'month'),
      _ when gap.inDays >= 1 => _plural(gap.inDays, 'day'),
      _ when gap.inHours >= 1 => _plural(gap.inHours, 'hour'),
      _ => _plural(gap.inMinutes, 'minute'),
    };

    return ahead ? 'In $amount' : '$amount ago';
  }

  // Calendar days, not difference.inDays — that counts 24-hour blocks and calls both 11pm tonight and 1am tomorrow "0 days".
  static int _calendarDaysFromNow(DateTime value, DateTime? now) {
    final current = now ?? DateTime.now();
    final today = DateTime(current.year, current.month, current.day);
    final local = value.toLocal();
    final that = DateTime(local.year, local.month, local.day);
    return that.difference(today).inDays;
  }

  static String _plural(int count, String unit) => '$count $unit${count == 1 ? '' : 's'}';
}
