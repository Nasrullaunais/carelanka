import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../validation/patient_fields.dart';

class BookAppointmentRequestDraft {
  const BookAppointmentRequestDraft({required this.scheduledAt, this.reason});

  final DateTime scheduledAt;
  final String? reason;
}

Future<BookAppointmentRequestDraft?> showBookAppointmentSheet(BuildContext context) {
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
      builder: (context, child) {
        return Theme(
          data: Theme.of(context).copyWith(
            colorScheme: Theme.of(context).colorScheme.copyWith(
                  primary: Theme.of(context).colorScheme.primary,
                ),
          ),
          child: child!,
        );
      },
    );
    if (picked != null) setState(() => _date = picked);
  }

  Future<void> _pickTime() async {
    final picked = await showTimePicker(
      context: context,
      initialTime: _time ?? const TimeOfDay(hour: 9, minute: 0),
      builder: (context, child) {
        return Theme(
          data: Theme.of(context).copyWith(
            colorScheme: Theme.of(context).colorScheme.copyWith(
                  primary: Theme.of(context).colorScheme.primary,
                ),
          ),
          child: child!,
        );
      },
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

    Navigator.of(context).pop(BookAppointmentRequestDraft(
      scheduledAt: scheduledAt,
      reason: _reason.text.trim().isEmpty ? null : _reason.text.trim(),
    ));
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final ready = _date != null && _time != null;
    final today = DateTime.now();

    return Padding(
      padding: EdgeInsets.only(
        left: 24,
        right: 24,
        top: 8,
        bottom: MediaQuery.of(context).viewInsets.bottom + 32,
      ),
      child: Form(
        key: _formKey,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text('Book a visit', style: theme.textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w800)),
              const SizedBox(height: 6),
              Text(
                'Only one open booking is allowed at a time.',
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: scheme.onSurfaceVariant,
                  height: 1.4,
                ),
              ),
              const SizedBox(height: 28),
              const _Label('Date'),
              const SizedBox(height: 12),
              Wrap(
                spacing: 10,
                runSpacing: 10,
                children: [
                  for (var offset = 1; offset <= 3; offset++)
                    _ChoiceChip(
                      label: FriendlyDate.relativeDay(today.add(Duration(days: offset))),
                      selected: _isSameDay(_date, today.add(Duration(days: offset))),
                      onTap: () => setState(
                        () => _date = today.add(Duration(days: offset)),
                      ),
                    ),
                  _ChoiceChip(
                    label: _date == null || _isSuggestedDay(_date!, today)
                        ? 'Another day'
                        : FriendlyDate.dayAndMonth(_date!),
                    selected: _date != null && !_isSuggestedDay(_date!, today),
                    icon: Icons.calendar_today_rounded,
                    onTap: _pickDate,
                  ),
                ],
              ),
              const SizedBox(height: 28),
              const _Label('Time'),
              const SizedBox(height: 12),
              Wrap(
                spacing: 10,
                runSpacing: 10,
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
                    icon: Icons.schedule_rounded,
                    onTap: _pickTime,
                  ),
                ],
              ),
              const SizedBox(height: 28),
              const _Label('Reason for visit'),
              const SizedBox(height: 12),
              TextFormField(
                controller: _reason,
                maxLines: 3,
                maxLength: PatientFieldLimits.visitReason,
                buildCounter: nearLimitCounter(),
                textCapitalization: TextCapitalization.sentences,
                validator: validateVisitReason,
                decoration: InputDecoration(
                  hintText: 'Optional',
                  alignLabelWithHint: true,
                  filled: true,
                  fillColor: scheme.surfaceContainerHighest.withValues(alpha: 0.3),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(AppTheme.radiusM),
                    borderSide: BorderSide(color: scheme.outlineVariant.withValues(alpha: 0.5)),
                  ),
                  enabledBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(AppTheme.radiusM),
                    borderSide: BorderSide(color: scheme.outlineVariant.withValues(alpha: 0.5)),
                  ),
                  focusedBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(AppTheme.radiusM),
                    borderSide: BorderSide(color: scheme.primary, width: 2),
                  ),
                  contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
                ),
              ),
              if (ready) ...[
                const SizedBox(height: 24),
                Container(
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    gradient: LinearGradient(
                      begin: Alignment.topLeft,
                      end: Alignment.bottomRight,
                      colors: [scheme.primary, scheme.primary.withValues(alpha: 0.8)],
                    ),
                    borderRadius: BorderRadius.circular(AppTheme.radiusM),
                    boxShadow: [
                      BoxShadow(
                        color: scheme.primary.withValues(alpha: 0.3),
                        blurRadius: 12,
                        offset: const Offset(0, 4),
                      ),
                    ],
                  ),
                  child: Row(
                    children: [
                      Container(
                        padding: const EdgeInsets.all(8),
                        decoration: BoxDecoration(
                          color: Colors.white.withValues(alpha: 0.2),
                          shape: BoxShape.circle,
                        ),
                        child: const Icon(Icons.check_rounded, size: 20, color: Colors.white),
                      ),
                      const SizedBox(width: 14),
                      Expanded(
                        child: Text(
                          '${FriendlyDate.relativeDay(_date!)}, ${_time!.format(context)}',
                          style: theme.textTheme.titleMedium?.copyWith(
                            color: Colors.white,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ),
                    ],
                  ),
                ).animate().fadeIn(duration: 300.ms).slideY(begin: 0.1),
              ],
              const SizedBox(height: 24),
              FilledButton(
                onPressed: ready ? _submit : null,
                style: FilledButton.styleFrom(
                  minimumSize: const Size.fromHeight(56),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppTheme.radiusM)),
                ),
                child: const Text('Book visit', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
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
      child: Text(
        text,
        style: Theme.of(context).textTheme.titleSmall?.copyWith(
          fontWeight: FontWeight.w700,
        ),
      ),
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

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(999),
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 200),
          padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 12),
          decoration: BoxDecoration(
            color: selected ? scheme.primary : scheme.surfaceContainerHighest.withValues(alpha: 0.3),
            borderRadius: BorderRadius.circular(999),
            border: Border.all(
              color: selected ? scheme.primary : scheme.outlineVariant.withValues(alpha: 0.5),
            ),
            boxShadow: selected
                ? [
                    BoxShadow(
                      color: scheme.primary.withValues(alpha: 0.3),
                      blurRadius: 8,
                      offset: const Offset(0, 2),
                    ),
                  ]
                : [],
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (icon != null) ...[
                Icon(icon, size: 16, color: selected ? scheme.onPrimary : scheme.onSurfaceVariant),
                const SizedBox(width: 8),
              ],
              Text(
                label,
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: selected ? scheme.onPrimary : scheme.onSurface,
                  fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
