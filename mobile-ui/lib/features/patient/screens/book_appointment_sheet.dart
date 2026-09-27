import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../validation/patient_fields.dart';
import '../widgets/pickers.dart';

class BookAppointmentRequestDraft {
  const BookAppointmentRequestDraft({required this.scheduledAt, this.reason});

  final DateTime scheduledAt;
  final String? reason;
}

Future<BookAppointmentRequestDraft?> showBookAppointmentSheet(
  BuildContext context,
) {
  return showModalBottomSheet<BookAppointmentRequestDraft>(
    context: context,
    isScrollControlled: true,
    builder: (_) => const _BookAppointmentSheet(),
  );
}

class _BookAppointmentSheet extends StatefulWidget {
  const _BookAppointmentSheet();

  @override
  State<_BookAppointmentSheet> createState() => _BookAppointmentSheetState();
}

class _BookAppointmentSheetState extends State<_BookAppointmentSheet> {
  final _formKey = GlobalKey<FormState>();
  final _reason = TextEditingController();

  DateTime? _date;
  TimeOfDay? _time;

  static const _stripDays = 14;

  static const _suggestedTimes = [
    TimeOfDay(hour: 8, minute: 0),
    TimeOfDay(hour: 9, minute: 0),
    TimeOfDay(hour: 10, minute: 0),
    TimeOfDay(hour: 11, minute: 0),
    TimeOfDay(hour: 13, minute: 0),
    TimeOfDay(hour: 14, minute: 0),
    TimeOfDay(hour: 15, minute: 0),
    TimeOfDay(hour: 16, minute: 0),
  ];

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  void _chooseDate(DateTime date) {
    setState(() {
      _date = date;
      if (_time != null && _isPast(date, _time!)) _time = null;
    });
  }

  Future<void> _pickDate() async {
    final now = DateTime.now();
    final picked = await showCalendarSheet(
      context,
      title: 'Pick a day',
      initial: _date ?? now.add(const Duration(days: 1)),
      first: now,
      last: now.add(const Duration(days: 365)),
    );
    if (picked != null) _chooseDate(picked);
  }

  Future<void> _pickTime() async {
    final picked = await showTimeWheelSheet(
      context,
      title: 'Pick a time',
      initial: _time ?? const TimeOfDay(hour: 9, minute: 0),
    );
    if (picked != null) setState(() => _time = picked);
  }

  static bool _isPast(DateTime date, TimeOfDay time) => DateTime(
    date.year,
    date.month,
    date.day,
    time.hour,
    time.minute,
  ).isBefore(DateTime.now());

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    if (_date == null || _time == null) return;

    final scheduledAt = DateTime(
      _date!.year,
      _date!.month,
      _date!.day,
      _time!.hour,
      _time!.minute,
    );

    if (scheduledAt.isBefore(DateTime.now())) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Select a date and time in the future.')),
      );
      return;
    }

    Navigator.of(context).pop(
      BookAppointmentRequestDraft(
        scheduledAt: scheduledAt,
        reason: _reason.text.trim().isEmpty ? null : _reason.text.trim(),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final ready = _date != null && _time != null;
    final today = DateTime.now();

    return Padding(
      padding: EdgeInsets.only(
        left: AppTheme.gutter,
        right: AppTheme.gutter,
        top: 4,
        bottom: MediaQuery.of(context).viewInsets.bottom + 28,
      ),
      child: Form(
        key: _formKey,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text('Book a visit', style: theme.textTheme.titleLarge),
              const SizedBox(height: 4),
              Text(
                'Only one open booking is allowed at a time.',
                style: theme.textTheme.bodySmall?.copyWith(
                  color: scheme.onSurfaceVariant,
                ),
              ),
              const SizedBox(height: 22),
              _Label(
                'Date',
                trailing: TextButton.icon(
                  onPressed: _pickDate,
                  icon: const Icon(Icons.calendar_month_outlined, size: 18),
                  label: const Text('Calendar'),
                ),
              ),
              const SizedBox(height: 6),
              _DayStrip(
                days: [
                  for (var offset = 1; offset <= _stripDays; offset++)
                    DateUtils.addDaysToDate(DateUtils.dateOnly(today), offset),
                ],
                selected: _date,
                onSelected: _chooseDate,
              ),
              const SizedBox(height: 22),
              const _Label('Time'),
              const SizedBox(height: 10),
              _TimeGrid(
                times: _suggestedTimes,
                selected: _time,
                isDisabled: (slot) => _date != null && _isPast(_date!, slot),
                onSelected: (slot) => setState(() => _time = slot),
              ),
              const SizedBox(height: 8),
              _OtherTimeButton(
                label: _time == null || _suggestedTimes.contains(_time)
                    ? 'Another time'
                    : _time!.format(context),
                selected: _time != null && !_suggestedTimes.contains(_time),
                onTap: _pickTime,
              ),
              const SizedBox(height: 22),
              const _Label('Reason for visit'),
              const SizedBox(height: 10),
              TextFormField(
                controller: _reason,
                maxLines: 3,
                maxLength: PatientFieldLimits.visitReason,
                buildCounter: nearLimitCounter(),
                textCapitalization: TextCapitalization.sentences,
                validator: validateVisitReason,
                decoration: const InputDecoration(
                  hintText: 'Optional',
                  alignLabelWithHint: true,
                ),
              ),
              const SizedBox(height: 20),
              if (ready)
                Container(
                  padding: const EdgeInsets.all(14),
                  decoration: BoxDecoration(
                    color: scheme.primaryContainer,
                    borderRadius: BorderRadius.circular(AppTheme.radiusM),
                  ),
                  child: Row(
                    children: [
                      Icon(
                        Icons.check_circle_outline,
                        size: 18,
                        color: scheme.onPrimaryContainer,
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Text(
                          '${FriendlyDate.relativeDay(_date!)}, ${_time!.format(context)}',
                          style: theme.textTheme.bodyMedium?.copyWith(
                            color: scheme.onPrimaryContainer,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: ready ? _submit : null,
                child: const Text('Book'),
              ),
            ],
          ),
        ),
      ),
    );
  }

}

class _Label extends StatelessWidget {
  const _Label(this.text, {this.trailing});

  final String text;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final label = Text(text, style: Theme.of(context).textTheme.titleSmall);
    if (trailing == null) return Align(alignment: Alignment.centerLeft, child: label);

    return Row(children: [Expanded(child: label), trailing!]);
  }
}

// A day picked from the calendar that is past the strip's fortnight joins the front of it, so the
// choice is still on screen; keying the list on it scrolls back to show it.
class _DayStrip extends StatelessWidget {
  const _DayStrip({required this.days, required this.selected, required this.onSelected});

  final List<DateTime> days;
  final DateTime? selected;
  final ValueChanged<DateTime> onSelected;

  @override
  Widget build(BuildContext context) {
    final offStrip = selected != null && !days.any((day) => DateUtils.isSameDay(day, selected))
        ? selected
        : null;
    final shown = [?offStrip, ...days];

    return SizedBox(
      height: 84,
      child: ListView.separated(
        key: ValueKey(offStrip),
        scrollDirection: Axis.horizontal,
        clipBehavior: Clip.none,
        itemCount: shown.length,
        separatorBuilder: (_, _) => const SizedBox(width: 8),
        itemBuilder: (context, index) {
          final day = shown[index];
          final isSelected = DateUtils.isSameDay(day, selected);
          return _Tile(
            width: 60,
            selected: isSelected,
            semanticsLabel: FriendlyDate.relativeDay(day),
            onTap: () => onSelected(day),
            child: _DayFace(day: day, selected: isSelected),
          );
        },
      ),
    );
  }
}

class _DayFace extends StatelessWidget {
  const _DayFace({required this.day, required this.selected});

  final DateTime day;
  final bool selected;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final strong = selected ? scheme.onPrimary : scheme.onSurface;
    final soft = selected ? scheme.onPrimary.withValues(alpha: 0.85) : scheme.onSurfaceVariant;

    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Text(
          DateFormat('EEE').format(day),
          style: theme.textTheme.labelMedium?.copyWith(color: soft),
        ),
        const SizedBox(height: 2),
        Text('${day.day}', style: theme.textTheme.titleLarge?.copyWith(color: strong, height: 1.2)),
        Text(
          DateFormat('MMM').format(day),
          style: theme.textTheme.labelMedium?.copyWith(color: soft),
        ),
      ],
    );
  }
}

class _TimeGrid extends StatelessWidget {
  const _TimeGrid({
    required this.times,
    required this.selected,
    required this.isDisabled,
    required this.onSelected,
  });

  final List<TimeOfDay> times;
  final TimeOfDay? selected;
  final bool Function(TimeOfDay slot) isDisabled;
  final ValueChanged<TimeOfDay> onSelected;

  static const _perRow = 4;

  @override
  Widget build(BuildContext context) {
    final rows = [
      for (var i = 0; i < times.length; i += _perRow)
        times.sublist(i, (i + _perRow).clamp(0, times.length)),
    ];

    return Column(
      children: [
        for (final row in rows) ...[
          if (row != rows.first) const SizedBox(height: 8),
          Row(
            children: [
              for (var i = 0; i < _perRow; i++) ...[
                if (i > 0) const SizedBox(width: 8),
                Expanded(
                  child: i < row.length ? _timeTile(context, row[i]) : const SizedBox(),
                ),
              ],
            ],
          ),
        ],
      ],
    );
  }

  Widget _timeTile(BuildContext context, TimeOfDay slot) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final isSelected = selected == slot;
    final disabled = isDisabled(slot);

    return _Tile(
      height: 46,
      selected: isSelected,
      semanticsLabel: slot.format(context),
      onTap: disabled ? null : () => onSelected(slot),
      child: Center(
        child: Text(
          slot.format(context),
          maxLines: 1,
          style: theme.textTheme.labelLarge?.copyWith(
            fontSize: 13.5,
            color: isSelected
                ? scheme.onPrimary
                : disabled
                ? scheme.onSurfaceVariant.withValues(alpha: 0.45)
                : scheme.onSurface,
          ),
        ),
      ),
    );
  }
}

class _OtherTimeButton extends StatelessWidget {
  const _OtherTimeButton({required this.label, required this.selected, required this.onTap});

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final colour = selected ? scheme.onPrimary : scheme.primary;

    return _Tile(
      height: 46,
      selected: selected,
      semanticsLabel: label,
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 14),
        child: Row(
          children: [
            Icon(Icons.schedule_outlined, size: 18, color: colour),
            const SizedBox(width: 10),
            Expanded(
              child: Text(label, style: theme.textTheme.labelLarge?.copyWith(color: colour)),
            ),
            Icon(Icons.chevron_right, size: 20, color: colour),
          ],
        ),
      ),
    );
  }
}

class _Tile extends StatelessWidget {
  const _Tile({
    required this.selected,
    required this.semanticsLabel,
    required this.onTap,
    required this.child,
    this.width,
    this.height,
  });

  final bool selected;
  final String semanticsLabel;
  final VoidCallback? onTap;
  final Widget child;
  final double? width;
  final double? height;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final radius = BorderRadius.circular(AppTheme.radiusM);

    return Semantics(
      button: true,
      selected: selected,
      enabled: onTap != null,
      label: semanticsLabel,
      excludeSemantics: true,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 180),
        curve: Curves.easeOut,
        width: width,
        height: height,
        decoration: BoxDecoration(
          color: selected ? scheme.primary : scheme.surfaceContainerLow,
          borderRadius: radius,
          border: Border.all(color: selected ? scheme.primary : scheme.outlineVariant),
        ),
        child: Material(
          type: MaterialType.transparency,
          child: InkWell(onTap: onTap, borderRadius: radius, child: child),
        ),
      ),
    );
  }
}

