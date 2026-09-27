import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../validation/patient_fields.dart';

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

  static const _suggestedTimes = [
    TimeOfDay(hour: 9, minute: 0),
    TimeOfDay(hour: 11, minute: 0),
    TimeOfDay(hour: 14, minute: 0),
    TimeOfDay(hour: 16, minute: 30),
  ];

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  Future<void> _pickDate() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _date ?? now.add(const Duration(days: 1)),
      firstDate: now,
      lastDate: now.add(const Duration(days: 365)),
      helpText: 'Pick a date',
    );
    if (picked != null) setState(() => _date = picked);
  }

  Future<void> _pickTime() async {
    final picked = await showTimePicker(
      context: context,
      initialTime: _time ?? const TimeOfDay(hour: 9, minute: 0),
    );
    if (picked != null) setState(() => _time = picked);
  }

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
              const _Label('Date'),
              const SizedBox(height: 10),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  for (var offset = 1; offset <= 3; offset++)
                    _ChoiceChip(
                      label: FriendlyDate.relativeDay(
                        today.add(Duration(days: offset)),
                      ),
                      selected: _isSameDay(
                        _date,
                        today.add(Duration(days: offset)),
                      ),
                      onTap: () => setState(
                        () => _date = today.add(Duration(days: offset)),
                      ),
                    ),
                  _ChoiceChip(
                    label: _date == null || _isSuggestedDay(_date!, today)
                        ? 'Another day'
                        : FriendlyDate.dayAndMonth(_date!),
                    selected: _date != null && !_isSuggestedDay(_date!, today),
                    icon: Icons.calendar_today_outlined,
                    onTap: _pickDate,
                  ),
                ],
              ),
              const SizedBox(height: 22),
              const _Label('Time'),
              const SizedBox(height: 10),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  for (final slot in _suggestedTimes)
                    _ChoiceChip(
                      label: slot.format(context),
                      selected: _time == slot,
                      onTap: () => setState(() => _time = slot),
                    ),
                  _ChoiceChip(
                    label: _time == null || _suggestedTimes.contains(_time)
                        ? 'Another time'
                        : _time!.format(context),
                    selected: _time != null && !_suggestedTimes.contains(_time),
                    icon: Icons.schedule_outlined,
                    onTap: _pickTime,
                  ),
                ],
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

  static bool _isSameDay(DateTime? a, DateTime b) =>
      a != null && a.year == b.year && a.month == b.month && a.day == b.day;

  static bool _isSuggestedDay(DateTime date, DateTime today) => List.generate(
    3,
    (index) => today.add(Duration(days: index + 1)),
  ).any((day) => _isSameDay(date, day));
}

class _Label extends StatelessWidget {
  const _Label(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: Alignment.centerLeft,
      child: Text(text, style: Theme.of(context).textTheme.titleSmall),
    );
  }
}

class _ChoiceChip extends StatelessWidget {
  const _ChoiceChip({
    required this.label,
    required this.selected,
    required this.onTap,
    this.icon,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return Semantics(
      button: true,
      selected: selected,
      label: label,
      child: Material(
        color: selected ? scheme.primary : scheme.surface,
        borderRadius: BorderRadius.circular(999),
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(999),
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 11),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(999),
              border: Border.all(
                color: selected ? scheme.primary : scheme.outlineVariant,
              ),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (icon != null) ...[
                  Icon(
                    icon,
                    size: 15,
                    color: selected
                        ? scheme.onPrimary
                        : scheme.onSurfaceVariant,
                  ),
                  const SizedBox(width: 7),
                ],
                Text(
                  label,
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: selected ? scheme.onPrimary : scheme.onSurface,
                    fontWeight: selected ? FontWeight.w600 : FontWeight.w500,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
