import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/app_theme.dart';

Future<DateTime?> showDateWheelSheet(
  BuildContext context, {
  required String title,
  required DateTime initial,
  required DateTime first,
  required DateTime last,
}) {
  final start = _clampDay(initial, first, last);
  return _showPickerSheet<DateTime>(
    context,
    title: title,
    initial: start,
    describe: (value) => DateFormat('EEEE, d MMMM yyyy').format(value),
    picker: (onChanged) => CupertinoDatePicker(
      mode: CupertinoDatePickerMode.date,
      dateOrder: DatePickerDateOrder.dmy,
      initialDateTime: start,
      minimumDate: _day(first),
      maximumDate: _day(last),
      onDateTimeChanged: onChanged,
    ),
  );
}

// Minutes move in steps of five: nobody books 10:37, and a 60-row wheel is slow to scroll.
Future<TimeOfDay?> showTimeWheelSheet(
  BuildContext context, {
  required String title,
  required TimeOfDay initial,
}) {
  final use24h = MediaQuery.of(context).alwaysUse24HourFormat;
  final start = DateTime(2000, 1, 1, initial.hour, initial.minute - initial.minute % 5);
  return _showPickerSheet<TimeOfDay>(
    context,
    title: title,
    initial: TimeOfDay.fromDateTime(start),
    describe: (value) => MaterialLocalizations.of(
      context,
    ).formatTimeOfDay(value, alwaysUse24HourFormat: use24h),
    picker: (onChanged) => CupertinoDatePicker(
      mode: CupertinoDatePickerMode.time,
      minuteInterval: 5,
      use24hFormat: use24h,
      initialDateTime: start,
      onDateTimeChanged: (value) => onChanged(TimeOfDay.fromDateTime(value)),
    ),
  );
}

// Closes on the tap that picks a day — the choice shows straight back on the form, so a
// separate Done press would only be a second tap for the same answer.
Future<DateTime?> showCalendarSheet(
  BuildContext context, {
  required String title,
  required DateTime initial,
  required DateTime first,
  required DateTime last,
}) {
  return showModalBottomSheet<DateTime>(
    context: context,
    isScrollControlled: true,
    builder: (sheetContext) => SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(AppTheme.gutter - 8, 0, AppTheme.gutter - 8, 12),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 8),
              child: Text(title, style: Theme.of(sheetContext).textTheme.titleLarge),
            ),
            CalendarDatePicker(
              initialDate: _clampDay(initial, first, last),
              firstDate: _day(first),
              lastDate: _day(last),
              onDateChanged: (picked) => Navigator.of(sheetContext).pop(picked),
            ),
          ],
        ),
      ),
    ),
  );
}

Future<T?> _showPickerSheet<T>(
  BuildContext context, {
  required String title,
  required T initial,
  required String Function(T value) describe,
  required Widget Function(ValueChanged<T> onChanged) picker,
}) {
  return showModalBottomSheet<T>(
    context: context,
    isScrollControlled: true,
    builder: (sheetContext) => _WheelSheet<T>(
      title: title,
      initial: initial,
      describe: describe,
      picker: picker,
    ),
  );
}

class _WheelSheet<T> extends StatefulWidget {
  const _WheelSheet({
    required this.title,
    required this.initial,
    required this.describe,
    required this.picker,
  });

  final String title;
  final T initial;
  final String Function(T value) describe;
  final Widget Function(ValueChanged<T> onChanged) picker;

  @override
  State<_WheelSheet<T>> createState() => _WheelSheetState<T>();
}

class _WheelSheetState<T> extends State<_WheelSheet<T>> {
  late T _value = widget.initial;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 0, AppTheme.gutter, 16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(widget.title, style: theme.textTheme.titleLarge),
            const SizedBox(height: 4),
            Text(
              widget.describe(_value),
              style: theme.textTheme.bodyLarge?.copyWith(
                color: scheme.primary,
                fontWeight: FontWeight.w600,
              ),
            ),
            const SizedBox(height: 12),
            SizedBox(
              height: 216,
              child: CupertinoTheme(
                data: CupertinoThemeData(
                  brightness: theme.brightness,
                  primaryColor: scheme.primary,
                  textTheme: CupertinoTextThemeData(
                    dateTimePickerTextStyle: theme.textTheme.titleLarge?.copyWith(
                      fontSize: 21,
                      fontWeight: FontWeight.w500,
                      color: scheme.onSurface,
                    ),
                  ),
                ),
                child: widget.picker((value) => setState(() => _value = value)),
              ),
            ),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: () => Navigator.of(context).pop(_value),
              child: const Text('Done'),
            ),
          ],
        ),
      ),
    );
  }
}

DateTime _day(DateTime value) => DateTime(value.year, value.month, value.day);

DateTime _clampDay(DateTime value, DateTime first, DateTime last) {
  final day = _day(value);
  if (day.isBefore(_day(first))) return _day(first);
  if (day.isAfter(_day(last))) return _day(last);
  return day;
}
