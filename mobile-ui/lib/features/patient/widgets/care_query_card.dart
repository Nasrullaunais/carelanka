import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../services/api_client/models/care_recommendation_status.dart';
import '../../../services/api_client/models/my_care_recommendation.dart';
import '../services/patient_service.dart';
import '../validation/patient_fields.dart';
import 'panels.dart';

/// A card inside My Stay, and nowhere else - it only exists while the patient is admitted, so
/// the 409 the server can return is a backstop rather than something ordinary use can reach.
/// Describes how the patient feels, in their own words, and starts the Patient Care Advisory
/// Agent. The agent's draft is never shown here - only a Doctor or Ward Nurse ever reads it,
/// and this screen shows nothing more than "reviewed" until they approve it.
class CareQueryCard extends StatefulWidget {
  const CareQueryCard({super.key});

  @override
  State<CareQueryCard> createState() => _CareQueryCardState();
}

class _CareQueryCardState extends State<CareQueryCard> {
  final _formKey = GlobalKey<FormState>();
  final _text = TextEditingController();

  bool _submitting = false;
  Future<List<MyCareRecommendation>>? _history;

  // Read once, from the same PatientService instance every other patient screen shares -
  // not a fresh one built from CareLankaApi, so a test's fake reaches this widget too.
  PatientService get _service => context.read<PatientService>();

  @override
  void initState() {
    super.initState();
    _loadHistory();
  }

  @override
  void dispose() {
    _text.dispose();
    super.dispose();
  }

  void _loadHistory() {
    setState(() {
      _history = _service.loadMyCareRecommendations().then((page) => page.items);
    });
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _submitting = true);

    try {
      await _service.submitCareQuery(_text.text);
      if (!mounted) return;

      _text.clear();
      _loadHistory();
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Sent. A nurse or doctor will look at this.'),
        ),
      );
    } on ApiException catch (error) {
      if (!mounted) return;

      final message = error.code == PatientService.notCurrentlyAdmittedForCareQueryCode
          ? 'This only works while you are admitted.'
          : error.message;

      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return SectionCard(
      title: 'How are you feeling?',
      icon: Icons.chat_bubble_outline,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            'A nurse or doctor will check it - this is not answered automatically, and it is '
            'not for an emergency.',
            style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
          const SizedBox(height: 14),
          Form(
            key: _formKey,
            child: TextFormField(
              controller: _text,
              minLines: 2,
              maxLines: 3,
              maxLength: PatientFieldLimits.careReportMax,
              buildCounter: nearLimitCounter(),
              textCapitalization: TextCapitalization.sentences,
              validator: validateCareReport,
              enabled: !_submitting,
              decoration: const InputDecoration(
                hintText: 'e.g. My headache is worse today.',
                alignLabelWithHint: true,
              ),
            ),
          ),
          const SizedBox(height: 8),
          Align(
            alignment: Alignment.centerRight,
            child: FilledButton.icon(
              onPressed: _submitting ? null : _submit,
              icon: _submitting
                  ? const SizedBox(
                      width: 16,
                      height: 16,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.send_outlined, size: 18),
              label: Text(_submitting ? 'Sending…' : 'Send'),
            ),
          ),
          const SizedBox(height: 4),
          _HistoryList(future: _history),
        ],
      ),
    );
  }
}

class _HistoryList extends StatelessWidget {
  const _HistoryList({required this.future});

  final Future<List<MyCareRecommendation>>? future;

  @override
  Widget build(BuildContext context) {
    if (future == null) return const SizedBox.shrink();

    return FutureBuilder<List<MyCareRecommendation>>(
      future: future,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const SizedBox.shrink();
        }

        final items = snapshot.data ?? const [];
        if (items.isEmpty) return const SizedBox.shrink();

        final theme = Theme.of(context);

        final shown = items.take(5).toList();

        return Padding(
          padding: const EdgeInsets.only(top: 18),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('What you have sent', style: theme.textTheme.titleSmall),
              for (var i = 0; i < shown.length; i++) ...[
                if (i > 0) const Divider(height: 28),
                if (i == 0) const SizedBox(height: 10),
                _HistoryRow(item: shown[i]),
              ],
            ],
          ),
        );
      },
    );
  }
}

class _HistoryRow extends StatelessWidget {
  const _HistoryRow({required this.item});

  final MyCareRecommendation item;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final status = item.status;

    final answered =
        status == CareRecommendationStatus.approved && item.doctorMessage != null;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _Label(
          text: item.reportedAt == null
              ? 'You asked'
              : 'You asked · ${FriendlyDate.relativeDayAndTime(item.reportedAt!)}',
        ),
        const SizedBox(height: 4),
        Text(item.reportedText ?? '', style: theme.textTheme.bodyMedium),
        const SizedBox(height: 10),
        if (answered) ...[
          _Label(text: 'Reply from the ward', color: scheme.primary),
          const SizedBox(height: 4),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: scheme.primaryContainer,
              borderRadius: BorderRadius.circular(AppTheme.radiusM),
            ),
            child: Text(
              item.doctorMessage!,
              style: theme.textTheme.bodyMedium?.copyWith(color: scheme.onPrimaryContainer),
            ),
          ),
        ] else
          _PendingNote(status: status),
      ],
    );
  }
}

/// Small caps-style caption that separates the patient's own words from the ward's reply. The
/// two used to run together with only a chip to tell them apart, which read as one block of text.
class _Label extends StatelessWidget {
  const _Label({required this.text, this.color});

  final String text;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Text(
      text.toUpperCase(),
      style: theme.textTheme.labelSmall?.copyWith(
        color: color ?? theme.colorScheme.onSurfaceVariant,
        fontWeight: FontWeight.w700,
        letterSpacing: 0.6,
      ),
    );
  }
}

/// What a patient sees before a reviewer has finished. A rejected report deliberately reads the
/// same as one still waiting: the reason is staff-facing and never reaches the patient.
class _PendingNote extends StatelessWidget {
  const _PendingNote({required this.status});

  final CareRecommendationStatus? status;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    final text = status == CareRecommendationStatus.rejected
        ? 'Checked by a nurse or doctor. They will follow up with you in person.'
        : 'Waiting for a nurse or doctor to check this.';

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(
          status == CareRecommendationStatus.rejected
              ? Icons.check_circle_outline
              : Icons.schedule,
          size: 15,
          color: scheme.onSurfaceVariant,
        ),
        const SizedBox(width: 6),
        Expanded(
          child: Text(
            text,
            style: theme.textTheme.bodySmall?.copyWith(color: scheme.onSurfaceVariant),
          ),
        ),
      ],
    );
  }
}

