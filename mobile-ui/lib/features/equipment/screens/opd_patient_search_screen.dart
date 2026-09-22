import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_view.dart';
import '../../../core/widgets/phone_width.dart';
import '../../../services/api_client/models/patient_summary.dart';
import '../services/lab_reports_service.dart';
import '../services/report_file_source.dart';
import '../state/opd_patient_search_controller.dart';
import '../state/patient_lab_reports_controller.dart';
import 'patient_lab_reports_screen.dart';

class OpdPatientSearchScreen extends StatelessWidget {
  const OpdPatientSearchScreen({super.key});

  void _open(BuildContext context, PatientSummary patient) {
    final service = context.read<LabReportsService>();
    final files = context.read<ReportFileSource>();

    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => ChangeNotifierProvider(
          create: (_) => PatientLabReportsController(
            service,
            files,
            patient: patient,
          )..loadReports(),
          child: const PatientLabReportsScreen(),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<OpdPatientSearchController>();
    final theme = Theme.of(context);
    final results = controller.results;

    return PhoneWidth(
      child: Scaffold(
        appBar: AppBar(title: const Text('OPD patient')),
        body: Column(
          children: [
            Padding(
              padding: const EdgeInsets.all(AppTheme.gutter),
              child: TextField(
                autofocus: true,
                decoration: const InputDecoration(
                  labelText: 'Patient code, name or NIC',
                  hintText: 'PB3MKWGJ',
                  prefixIcon: Icon(Icons.search),
                ),
                textInputAction: TextInputAction.search,
                onChanged: controller.setSearch,
              ),
            ),
            Expanded(
              child: switch (results) {
                null => const EmptyView(
                    icon: Icons.person_search_outlined,
                    title: 'Find the patient',
                    message: 'Type at least ${OpdPatientSearchController.minSearchLength} '
                        'characters of the code on the specimen label, the name or the NIC.',
                  ),
                _ => AsyncView<List<PatientSummary>>(
                    state: results,
                    onRetry: controller.retry,
                    builder: (context, patients) {
                      if (patients.isEmpty) {
                        return const EmptyView(
                          icon: Icons.person_off_outlined,
                          title: 'No patient found',
                          message: 'Check the code on the specimen label and try again.',
                        );
                      }

                      return ListView.separated(
                        padding: const EdgeInsets.symmetric(horizontal: 12),
                        itemCount: patients.length,
                        separatorBuilder: (_, __) => const Divider(height: 1),
                        itemBuilder: (context, index) {
                          final patient = patients[index];
                          final nic = patient.nic;

                          return ListTile(
                            onTap: () => _open(context, patient),
                            leading: CircleAvatar(
                              backgroundColor: theme.colorScheme.secondaryContainer,
                              child: Icon(
                                Icons.person_outline,
                                color: theme.colorScheme.onSecondaryContainer,
                              ),
                            ),
                            title: Text(patient.fullName),
                            subtitle: Text(
                              nic == null ? patient.patientCode : '${patient.patientCode} · NIC $nic',
                            ),
                            trailing: const Icon(Icons.chevron_right),
                          );
                        },
                      );
                    },
                  ),
              },
            ),
          ],
        ),
      ),
    );
  }
}
