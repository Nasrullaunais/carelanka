import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/phone_width.dart';
import '../services/lab_reports_service.dart';
import '../services/report_file_source.dart';
import '../state/opd_patient_search_controller.dart';
import 'opd_patient_search_screen.dart';

class LabReportsScreen extends StatelessWidget {
  const LabReportsScreen({super.key});

  void _openOpdSearch(BuildContext context) {
    final service = context.read<LabReportsService>();
    final files = context.read<ReportFileSource>();

    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => MultiProvider(
          providers: [
            Provider.value(value: service),
            Provider.value(value: files),
            ChangeNotifierProvider(create: (_) => OpdPatientSearchController(service)),
          ],
          child: const OpdPatientSearchScreen(),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return PhoneWidth(
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Lab reports'),
          actions: [
            IconButton(
              icon: const Icon(Icons.logout),
              tooltip: 'Sign out',
              onPressed: () => context.read<AuthController>().signOut(),
            ),
          ],
        ),
        body: ListView(
          padding: const EdgeInsets.all(AppTheme.gutter),
          children: [
            Card(
              clipBehavior: Clip.antiAlias,
              child: InkWell(
                onTap: () => _openOpdSearch(context),
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Row(
                    children: [
                      Container(
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: theme.colorScheme.primaryContainer,
                          borderRadius: BorderRadius.circular(AppTheme.radiusM),
                        ),
                        child: Icon(
                          Icons.science_outlined,
                          color: theme.colorScheme.onPrimaryContainer,
                        ),
                      ),
                      const SizedBox(width: 16),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text('OPD patient', style: theme.textTheme.titleMedium),
                            const SizedBox(height: 4),
                            Text(
                              'A discharged patient who gave blood at the OPD lab. '
                              'Find them and upload their report.',
                              style: theme.textTheme.bodyMedium?.copyWith(
                                color: theme.colorScheme.onSurfaceVariant,
                              ),
                            ),
                          ],
                        ),
                      ),
                      const Icon(Icons.chevron_right),
                    ],
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
