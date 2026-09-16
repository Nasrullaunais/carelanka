import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_view.dart';
import '../../../core/widgets/phone_width.dart';
import '../../../services/api_client/models/lab_report.dart';
import '../state/patient_lab_reports_controller.dart';
import 'upload_lab_report_screen.dart';

class PatientLabReportsScreen extends StatelessWidget {
  const PatientLabReportsScreen({super.key});

  Future<void> _openUpload(BuildContext context) async {
    final controller = context.read<PatientLabReportsController>();
    final messenger = ScaffoldMessenger.of(context);

    final uploaded = await Navigator.of(context).push<LabReport>(
      MaterialPageRoute(
        builder: (_) => ChangeNotifierProvider.value(
          value: controller,
          child: const UploadLabReportScreen(),
        ),
      ),
    );

    if (uploaded != null) {
      messenger.showSnackBar(
        SnackBar(content: Text('${uploaded.testName} uploaded for ${controller.patient.fullName}.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<PatientLabReportsController>();
    final patient = controller.patient;
    final theme = Theme.of(context);

    return PhoneWidth(
      child: Scaffold(
        appBar: AppBar(title: Text(patient.fullName)),
        floatingActionButton: FloatingActionButton.extended(
          onPressed: () => _openUpload(context),
          icon: const Icon(Icons.upload_file),
          label: const Text('Upload lab report'),
        ),
        body: RefreshIndicator(
          onRefresh: controller.loadReports,
          child: ListView(
            padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 0, AppTheme.gutter, 96),
            children: [
              Text(
                patient.nic == null ? patient.patientCode : '${patient.patientCode} · NIC ${patient.nic}',
                style: theme.textTheme.bodyMedium
                    ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
              const SizedBox(height: AppTheme.gutter),
              Text('Lab reports', style: theme.textTheme.titleMedium),
              const SizedBox(height: 8),
              AsyncView<List<LabReport>>(
                state: controller.reports,
                onRetry: controller.loadReports,
                loading: const Column(
                  children: [
                    Skeleton(height: 72),
                    SizedBox(height: 10),
                    Skeleton(height: 72),
                  ],
                ),
                builder: (context, reports) {
                  if (reports.isEmpty) {
                    return const Padding(
                      padding: EdgeInsets.only(top: 32),
                      child: EmptyView(
                        icon: Icons.description_outlined,
                        title: 'No lab reports yet',
                        message: 'Reports you upload for this patient appear here.',
                      ),
                    );
                  }

                  return Column(
                    children: [
                      for (final report in reports) ...[
                        _ReportTile(report: report),
                        const SizedBox(height: 10),
                      ],
                    ],
                  );
                },
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ReportTile extends StatelessWidget {
  const _ReportTile({required this.report});

  final LabReport report;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final summary = report.summary;
    final isPdf = report.contentType == 'application/pdf';

    return Card(
      child: ListTile(
        leading: Icon(
          isPdf ? Icons.picture_as_pdf_outlined : Icons.image_outlined,
          color: theme.colorScheme.primary,
        ),
        title: Text(report.testName),
        subtitle: Text(
          summary == null
              ? FriendlyDate.full(report.createdAt)
              : '${FriendlyDate.full(report.createdAt)}\n$summary',
        ),
        isThreeLine: summary != null,
      ),
    );
  }
}
