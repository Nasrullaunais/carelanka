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
            'Tell us in your own words. A nurse or doctor will check it - this is not '
            'answered automatically, and it is not for an emergency.',
            style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
          const SizedBox(height: 14),
          Form(
            key: _formKey,
            child: TextFormField(
              controller: _text,
              maxLines: 4,
              maxLength: PatientFieldLimits.careReportMax,
              buildCounter: nearLimitCounter(),
              textCapitalization: TextCapitalization.sentences,
              validator: validateCareReport,
              enabled: !_submitting,
              decoration: const InputDecoration(
                hintText: "e.g. My headache is worse today and it hurts more when I lie flat.",
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

        return Padding(
          padding: const EdgeInsets.only(top: 14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('What you have sent', style: theme.textTheme.titleSmall),
              const SizedBox(height: 6),
              for (final item in items.take(5)) _HistoryRow(item: item),
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

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Text(item.reportedText ?? '', style: theme.textTheme.bodyMedium),
              ),
              const SizedBox(width: 8),
              _StatusChip(status: status),
            ],
          ),
          if (item.reportedAt != null)
            Padding(
              padding: const EdgeInsets.only(top: 2),
              child: Text(
                FriendlyDate.relativeDayAndTime(item.reportedAt!),
                style: theme.textTheme.bodySmall?.copyWith(color: scheme.onSurfaceVariant),
              ),
            ),
          if (status == CareRecommendationStatus.approved && item.doctorMessage != null) ...[
            const SizedBox(height: 6),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: scheme.primaryContainer,
                borderRadius: BorderRadius.circular(AppTheme.radiusM),
              ),
              child: Text(
                item.doctorMessage!,
                style: theme.textTheme.bodySmall?.copyWith(color: scheme.onPrimaryContainer),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _StatusChip extends StatelessWidget {
  const _StatusChip({required this.status});

  final CareRecommendationStatus? status;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    final (label, color) = switch (status) {
      CareRecommendationStatus.approved => ('Reviewed', scheme.primary),
      CareRecommendationStatus.rejected => ('Reviewed', scheme.onSurfaceVariant),
      _ => ('Awaiting review', scheme.onSurfaceVariant),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: Theme.of(context)
            .textTheme
            .labelSmall
            ?.copyWith(color: color, fontWeight: FontWeight.w600),
      ),
    );
  }
}
