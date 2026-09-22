import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/phone_width.dart';
import '../state/patient_lab_reports_controller.dart';
import '../widgets/report_attachment.dart';

class UploadLabReportScreen extends StatefulWidget {
  const UploadLabReportScreen({super.key});

  @override
  State<UploadLabReportScreen> createState() => _UploadLabReportScreenState();
}

class _UploadLabReportScreenState extends State<UploadLabReportScreen> {
  final _testName = TextEditingController();
  final _summary = TextEditingController();

  String? _problem;

  @override
  void dispose() {
    _testName.dispose();
    _summary.dispose();
    super.dispose();
  }

  Future<void> _upload() async {
    final controller = context.read<PatientLabReportsController>();

    final problem = controller.validate(testName: _testName.text, summary: _summary.text);
    setState(() => _problem = problem);
    if (problem != null) return;

    final report = await controller.upload(testName: _testName.text, summary: _summary.text);
    if (!mounted || report == null) return;

    Navigator.of(context).pop(report);
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<PatientLabReportsController>();
    final patient = controller.patient;
    final failure = controller.uploadFailure;
    final theme = Theme.of(context);

    return PhoneWidth(
      child: Scaffold(
        appBar: AppBar(title: const Text('Upload lab report')),
        body: ListView(
          padding: const EdgeInsets.all(AppTheme.gutter),
          children: [
            Card(
              child: ListTile(
                leading: const Icon(Icons.person_outline),
                title: Text(patient.fullName),
                subtitle: Text(patient.patientCode),
              ),
            ),
            const SizedBox(height: AppTheme.gutter),
            TextField(
              controller: _testName,
              enabled: !controller.uploading,
              decoration: const InputDecoration(
                labelText: 'Test name',
                hintText: 'Full blood count',
              ),
              maxLength: 120,
              textInputAction: TextInputAction.next,
            ),
            TextField(
              controller: _summary,
              enabled: !controller.uploading,
              decoration: const InputDecoration(
                labelText: 'Summary (optional)',
                alignLabelWithHint: true,
              ),
              maxLength: 1000,
              maxLines: 3,
            ),
            const SizedBox(height: 8),
            ReportAttachment(
              attachment: controller.attachment,
              enabled: !controller.uploading,
              onPhotograph: controller.capturePhoto,
              onAttachPdf: controller.pickDocument,
              onRemove: controller.removeAttachment,
            ),
            if (_problem != null) ...[
              const SizedBox(height: 12),
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
              onPressed: controller.uploading ? null : _upload,
              icon: controller.uploading
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.upload_file),
              label: Text(controller.uploading ? 'Uploading…' : 'Upload report'),
              style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(52)),
            ),
          ],
        ),
      ),
    );
  }
}
