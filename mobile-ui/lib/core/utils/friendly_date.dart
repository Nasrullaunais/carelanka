import 'package:intl/intl.dart';

/// Dates written the way a person would say them.
///
/// Everything here takes UTC off the wire and renders local time — the API
/// sends `DateTimeOffset` in UTC, so calling `.toLocal()` is not optional.
class FriendlyDate {
  const FriendlyDate._();

  /// `18/09/2026`
  static String date(DateTime value) => DateFormat('dd/MM/yyyy').format(value.toLocal());

  /// `Fri, 18 Sep`
  static String dayAndMonth(DateTime value) => DateFormat('EEE, d MMM').format(value.toLocal());

  /// `9:00 AM`
  static String time(DateTime value) => DateFormat('h:mm a').format(value.toLocal());

  /// `Fri, 18 Sep 2026 at 9:00 AM`
  static String full(DateTime value) =>
      '${DateFormat('EEE, d MMM yyyy').format(value.toLocal())} at ${time(value)}';

  /// `Today`, `Tomorrow`, `Yesterday`, else `Fri, 18 Sep`.
  static String relativeDay(DateTime value, {DateTime? now}) {
    final days = _calendarDaysFromNow(value, now);
    return switch (days) {
      0 => 'Today',
      1 => 'Tomorrow',
      -1 => 'Yesterday',
      _ => dayAndMonth(value),
    };
  }

  /// `Today at 9:00 AM`, `Tomorrow at 9:00 AM`, `Fri, 18 Sep at 9:00 AM`.
  static String relativeDayAndTime(DateTime value, {DateTime? now}) =>
      '${relativeDay(value, now: now)} at ${time(value)}';

  /// How far away it is, as a phrase you could say out loud: `In 4 days`,
  /// `In 2 hours`, `Now`, `3 days ago`.
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

  /// Whole calendar days between today and [value] — not `difference.inDays`,
  /// which counts 24-hour blocks and so calls 11pm tonight "0 days" and 1am
  /// tomorrow "0 days" as well.
  static int _calendarDaysFromNow(DateTime value, DateTime? now) {
    final current = now ?? DateTime.now();
    final today = DateTime(current.year, current.month, current.day);
    final local = value.toLocal();
    final that = DateTime(local.year, local.month, local.day);
    return that.difference(today).inDays;
  }

  static String _plural(int count, String unit) => '$count $unit${count == 1 ? '' : 's'}';
}
