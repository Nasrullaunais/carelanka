import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/file_opener.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/my_lab_report.dart';
import '../services/patient_service.dart';
import '../state/lab_reports_controller.dart';

class MyReportsScreen extends StatelessWidget {
  const MyReportsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => LabReportsController(PatientService(context.read<CareLankaApi>()))
        ..load(),
      child: const _MyReportsView(),
    );
  }
}

class _MyReportsView extends StatelessWidget {
  const _MyReportsView();

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<LabReportsController>();

    return Scaffold(
      appBar: AppBar(title: const Text('My reports')),
      body: AsyncView<List<MyLabReport>>(
        state: controller.reports,
        onRetry: controller.load,
        loading: const _ListSkeleton(),
        builder: (context, reports) {
          if (reports.isEmpty) {
            return RefreshableMessage(
              onRefresh: controller.load,
              child: const EmptyView(
                icon: Icons.description_outlined,
                title: 'No reports yet',
                message: 'Results the laboratory files for you will appear here.',
              ),
            );
          }

          return RefreshIndicator(
            onRefresh: controller.load,
            child: ListView.separated(
              padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 12, AppTheme.gutter, 32),
              itemCount: reports.length,
              separatorBuilder: (_, __) => const SizedBox(height: 10),
              itemBuilder: (_, index) => _ReportTile(report: reports[index]),
            ),
          );
        },
      ),
    );
  }
}

class _ReportTile extends StatefulWidget {
  const _ReportTile({required this.report});

  final MyLabReport report;

  @override
  State<_ReportTile> createState() => _ReportTileState();
}

class _ReportTileState extends State<_ReportTile> {
  bool _opening = false;

  Future<void> _open() async {
    if (!isFileOpenerSupported) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
        content: Text('Viewing a report is only built for the web app so far.'),
      ));
      return;
    }

    setState(() => _opening = true);

    try {
      final bytes = await downloadBytes(
        context.read<Dio>(),
        PatientService.labReportFilePath(widget.report.id),
      );
      await openFileBytes(
        bytes: bytes,
        contentType: widget.report.contentType,
        fileName: widget.report.fileName,
      );
    } on ApiException catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.message)));
    } finally {
      if (mounted) setState(() => _opening = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final report = widget.report;
    final isPdf = report.contentType == 'application/pdf';
    final summary = report.summary;

    return Card(
      child: ListTile(
        onTap: _opening ? null : _open,
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
        trailing: _opening
            ? const SizedBox(
                width: 20,
                height: 20,
                child: CircularProgressIndicator(strokeWidth: 2),
              )
            : const Icon(Icons.chevron_right),
      ),
    );
  }
}

class _ListSkeleton extends StatelessWidget {
  const _ListSkeleton();

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 12, AppTheme.gutter, 32),
      children: const [
        Skeleton(height: 72, radius: AppTheme.radiusL),
        SizedBox(height: 10),
        Skeleton(height: 72, radius: AppTheme.radiusL),
      ],
    );
  }
}
