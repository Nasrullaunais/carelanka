import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_data.dart';
import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/models/my_prescription.dart';
import '../../../services/api_client/models/prescription_status.dart';
import '../services/prescription_service.dart';
import '../services/report_file_source.dart';
import '../state/my_prescriptions_controller.dart';
import '../widgets/report_attachment.dart';

/// The patient's Prescriptions tab. Owned by Equipment Management - the pharmacy is ours - and
/// placed in the patient app's tab bar by `PatientShell`, which also provides [PrescriptionService].
class MyPrescriptionsTab extends StatefulWidget {
  const MyPrescriptionsTab({super.key, this.files});

  /// Replaced in tests; the device camera and file picker otherwise.
  final ReportFileSource? files;

  @override
  State<MyPrescriptionsTab> createState() => MyPrescriptionsTabState();
}

class MyPrescriptionsTabState extends State<MyPrescriptionsTab> {
  late final MyPrescriptionsController _controller;

  @override
  void initState() {
    super.initState();
    _controller = MyPrescriptionsController(
      context.read<PrescriptionService>(),
      widget.files ?? DeviceReportFileSource(),
    )..load();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  /// Called when the tab is opened, so a token the pharmacy issued meanwhile shows up.
  Future<void> refresh() => _controller.load(showLoading: false);

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider.value(
      value: _controller,
      child: const MyPrescriptionsScreen(),
    );
  }
}

class MyPrescriptionsScreen extends StatelessWidget {
  const MyPrescriptionsScreen({super.key});

  Future<void> _openUpload(BuildContext context) async {
    final controller = context.read<MyPrescriptionsController>();
    final messenger = ScaffoldMessenger.of(context);
    controller.clearForm();

    final sent = await Navigator.of(context).push<MyPrescription>(
      MaterialPageRoute(
        builder: (_) => ChangeNotifierProvider.value(
          value: controller,
          child: const UploadPrescriptionScreen(),
        ),
      ),
    );

    if (sent != null) {
      messenger.showSnackBar(const SnackBar(
        content: Text('Prescription sent. You will get a token here when it is ready.'),
      ));
    }
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<MyPrescriptionsController>();
    final state = controller.prescriptions;
    final unlinked =
        state is AsyncFailed<List<MyPrescription>> && state.error.code == 'cl_pat_033';
    Future<void> refresh() => controller.load(showLoading: false);

    return Scaffold(
      appBar: AppBar(title: const Text('Prescriptions')),
      floatingActionButton: unlinked
          ? null
          : FloatingActionButton.extended(
              onPressed: () => _openUpload(context),
              icon: const Icon(Icons.upload_file),
              label: const Text('Upload prescription'),
            ),
      body: unlinked
          ? RefreshableMessage(
              onRefresh: refresh,
              child: const EmptyView(
                icon: Icons.badge_outlined,
                title: 'Complete your details',
                message: 'Add your details on the Profile tab before sending a prescription.',
              ),
            )
          : AsyncView<List<MyPrescription>>(
              state: state,
              onRetry: controller.load,
              loading: const Padding(
                padding: EdgeInsets.all(AppTheme.gutter),
                child: Column(
                  children: [
                    Skeleton(height: 110),
                    SizedBox(height: 10),
                    Skeleton(height: 110),
                  ],
                ),
              ),
              builder: (context, prescriptions) {
                if (prescriptions.isEmpty) {
                  return RefreshableMessage(
                    onRefresh: refresh,
                    child: const EmptyView(
                      icon: Icons.medication_outlined,
                      title: 'No prescriptions yet',
                      message: 'Send a photo of your prescription. The pharmacy gets your '
                          'medicine ready and gives you a token, so you can collect it '
                          'without waiting in the queue.',
                    ),
                  );
                }

                return RefreshIndicator(
                  onRefresh: refresh,
                  child: ListView.separated(
                    padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 12, AppTheme.gutter, 96),
                    itemCount: prescriptions.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 10),
                    itemBuilder: (_, index) => PrescriptionCard(prescription: prescriptions[index]),
                  ),
                );
              },
            ),
    );
  }
}

class PrescriptionCard extends StatelessWidget {
  const PrescriptionCard({super.key, required this.prescription});

  final MyPrescription prescription;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final note = prescription.note;
    final token = prescription.tokenNumber;

    final (label, color, icon) = switch (prescription.status) {
      PrescriptionStatus.ready => ('Ready to collect', scheme.primary, Icons.inventory_2_outlined),
      PrescriptionStatus.delivered => ('Delivered', scheme.tertiary, Icons.check_circle_outline),
      PrescriptionStatus.rejected => ("Can't be filled", scheme.error, Icons.error_outline),
      _ => ('Waiting for the pharmacy', scheme.onSurfaceVariant, Icons.hourglass_top),
    };

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Icon(icon, color: color),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(label, style: theme.textTheme.titleMedium?.copyWith(color: color)),
                ),
              ],
            ),
            const SizedBox(height: 4),
            Text(
              'Sent ${FriendlyDate.full(prescription.createdAt)}',
              style: theme.textTheme.bodySmall?.copyWith(color: scheme.onSurfaceVariant),
            ),
            if (note != null) ...[
              const SizedBox(height: 6),
              Text(note, style: theme.textTheme.bodyMedium),
            ],
            if (prescription.status == PrescriptionStatus.ready && token != null) ...[
              const SizedBox(height: 12),
              Container(
                padding: const EdgeInsets.symmetric(vertical: 14),
                decoration: BoxDecoration(
                  color: scheme.primaryContainer,
                  borderRadius: BorderRadius.circular(AppTheme.radiusM),
                ),
                child: Column(
                  children: [
                    Text(
                      'Your token',
                      style: theme.textTheme.labelLarge?.copyWith(color: scheme.onPrimaryContainer),
                    ),
                    Text(
                      '$token',
                      style: theme.textTheme.displaySmall?.copyWith(
                        color: scheme.onPrimaryContainer,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    Text(
                      'Show this at the pharmacy counter to collect your medicine.',
                      textAlign: TextAlign.center,
                      style: theme.textTheme.bodySmall?.copyWith(color: scheme.onPrimaryContainer),
                    ),
                  ],
                ),
              ),
            ],
            if (prescription.status == PrescriptionStatus.delivered) ...[
              const SizedBox(height: 6),
              Text(
                [
                  if (token != null) 'Token $token',
                  if (prescription.deliveredAt != null)
                    'collected ${FriendlyDate.full(prescription.deliveredAt!)}',
                ].join(' · '),
                style: theme.textTheme.bodyMedium,
              ),
            ],
            if (prescription.status == PrescriptionStatus.rejected &&
                prescription.rejectionReason != null) ...[
              const SizedBox(height: 6),
              Text(prescription.rejectionReason!, style: theme.textTheme.bodyMedium),
            ],
          ],
        ),
      ),
    );
  }
}

class UploadPrescriptionScreen extends StatefulWidget {
  const UploadPrescriptionScreen({super.key});

  @override
  State<UploadPrescriptionScreen> createState() => _UploadPrescriptionScreenState();
}

class _UploadPrescriptionScreenState extends State<UploadPrescriptionScreen> {
  final _note = TextEditingController();
  String? _problem;

  @override
  void dispose() {
    _note.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final controller = context.read<MyPrescriptionsController>();

    final problem = controller.validate(note: _note.text);
    setState(() => _problem = problem);
    if (problem != null) return;

    final sent = await controller.upload(note: _note.text);
    if (!mounted || sent == null) return;

    Navigator.of(context).pop(sent);
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<MyPrescriptionsController>();
    final ApiException? failure = controller.uploadFailure;
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: const Text('Upload prescription')),
      body: ListView(
        padding: const EdgeInsets.all(AppTheme.gutter),
        children: [
          Text(
            'Take a clear photo of the whole prescription, or attach it as a PDF.',
            style: theme.textTheme.bodyMedium,
          ),
          const SizedBox(height: AppTheme.gutter),
          ReportAttachment(
            attachment: controller.attachment,
            enabled: !controller.uploading,
            onPhotograph: controller.capturePhoto,
            onAttachPdf: controller.pickDocument,
            onRemove: controller.removeAttachment,
            photographLabel: 'Photograph the prescription',
          ),
          const SizedBox(height: AppTheme.gutter),
          TextField(
            controller: _note,
            enabled: !controller.uploading,
            decoration: const InputDecoration(
              labelText: 'Note for the pharmacy (optional)',
              hintText: 'I will collect it after 4pm.',
              alignLabelWithHint: true,
            ),
            maxLength: 500,
            maxLines: 3,
          ),
          if (_problem != null) ...[
            const SizedBox(height: 8),
            Text(_problem!, style: TextStyle(color: theme.colorScheme.error)),
          ],
          if (failure != null) ...[
            const SizedBox(height: 12),
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: theme.colorScheme.errorContainer,
                borderRadius: BorderRadius.circular(AppTheme.radiusM),
              ),
              child: Text(
                failure.message,
                style: TextStyle(color: theme.colorScheme.onErrorContainer),
              ),
            ),
          ],
          const SizedBox(height: AppTheme.gutter),
          FilledButton.icon(
            onPressed: controller.uploading ? null : _send,
            icon: controller.uploading
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Icon(Icons.send),
            label: Text(controller.uploading ? 'Sending…' : 'Send to the pharmacy'),
            style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(52)),
          ),
        ],
      ),
    );
  }
}
