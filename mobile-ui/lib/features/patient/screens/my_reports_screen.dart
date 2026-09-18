import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/file_opener.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_view.dart';
import '../../../core/widgets/phone_width.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/my_lab_report.dart';
import '../services/patient_service.dart';
import '../state/lab_reports_controller.dart';

class MyReportsScreen extends StatelessWidget {
  const MyReportsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) =>
          LabReportsController(PatientService(context.read<CareLankaApi>()))
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
    final theme = Theme.of(context);

    return PhoneWidth(
      child: Scaffold(
        appBar: AppBar(
          backgroundColor: Colors.transparent,
          surfaceTintColor: Colors.transparent,
          elevation: 0,
        ),
        extendBodyBehindAppBar: true,
        body: CustomScrollView(
          slivers: [
            SliverToBoxAdapter(
              child: SafeArea(
                bottom: false,
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 16, AppTheme.gutter, 16),
                  child: Text(
                    'My Reports',
                    style: theme.textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.w800),
                  ).animate().fadeIn().slideY(begin: -0.2),
                ),
              ),
            ),
            SliverFillRemaining(
              hasScrollBody: false,
              child: AsyncView<List<MyLabReport>>(
                state: controller.reports,
                onRetry: controller.load,
                loading: const _ListSkeleton(),
                builder: (context, reports) {
                  if (reports.isEmpty) {
                    return RefreshableMessage(
                      onRefresh: controller.load,
                      child: const EmptyView(
                        icon: Icons.description_rounded,
                        title: 'No reports yet',
                        message: 'Results the laboratory files for you will appear here.',
                      ).animate().fadeIn(),
                    );
                  }

                  return RefreshIndicator(
                    onRefresh: controller.load,
                    child: ListView.separated(
                      padding: const EdgeInsets.fromLTRB(
                        AppTheme.gutter,
                        4,
                        AppTheme.gutter,
                        32,
                      ),
                      physics: const NeverScrollableScrollPhysics(),
                      shrinkWrap: true,
                      itemCount: reports.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 12),
                      itemBuilder: (_, index) => _ReportTile(report: reports[index])
                          .animate()
                          .fadeIn(delay: (50 * index).ms)
                          .slideY(begin: 0.1),
                    ),
                  );
                },
              ),
            ),
          ],
        ),
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
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'Viewing a report is only built for the web app so far.',
          ),
        ),
      );
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
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text(error.message)));
    } finally {
      if (mounted) setState(() => _opening = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final report = widget.report;
    final isPdf = report.contentType == 'application/pdf';
    final summary = report.summary;

    return Container(
      decoration: BoxDecoration(
        color: scheme.surface,
        borderRadius: BorderRadius.circular(AppTheme.radiusM),
        border: Border.all(color: scheme.outlineVariant.withValues(alpha: 0.15)),
        boxShadow: [
          BoxShadow(
            color: scheme.shadow.withValues(alpha: 0.05),
            blurRadius: 10,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: _opening ? null : _open,
          borderRadius: BorderRadius.circular(AppTheme.radiusM),
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Row(
              children: [
                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: scheme.primaryContainer.withValues(alpha: 0.5),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Icon(
                    isPdf ? Icons.picture_as_pdf_rounded : Icons.image_rounded,
                    color: scheme.primary,
                    size: 24,
                  ),
                ),
                const SizedBox(width: 16),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        report.testName,
                        style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        FriendlyDate.full(report.createdAt),
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: scheme.onSurfaceVariant,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                      if (summary != null) ...[
                        const SizedBox(height: 4),
                        Text(
                          summary,
                          style: theme.textTheme.bodyMedium?.copyWith(
                            color: scheme.onSurfaceVariant,
                            height: 1.4,
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
                const SizedBox(width: 12),
                if (_opening)
                  const SizedBox(
                    width: 24,
                    height: 24,
                    child: CircularProgressIndicator(strokeWidth: 2.5),
                  )
                else
                  Container(
                    padding: const EdgeInsets.all(6),
                    decoration: BoxDecoration(
                      color: scheme.surfaceContainerHighest.withValues(alpha: 0.5),
                      shape: BoxShape.circle,
                    ),
                    child: Icon(
                      Icons.chevron_right_rounded,
                      color: scheme.onSurfaceVariant,
                      size: 20,
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

class _ListSkeleton extends StatelessWidget {
  const _ListSkeleton();

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(
        AppTheme.gutter,
        12,
        AppTheme.gutter,
        32,
      ),
      children: const [
        Skeleton(height: 88, radius: AppTheme.radiusM),
        SizedBox(height: 12),
        Skeleton(height: 88, radius: AppTheme.radiusM),
        SizedBox(height: 12),
        Skeleton(height: 88, radius: AppTheme.radiusM),
      ],
    );
  }
}
