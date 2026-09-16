import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/widgets/async_view.dart';
import '../../../core/widgets/phone_width.dart';
import '../../../services/api_client/models/worklist_row.dart';
import '../state/worklist_controller.dart';
import '../widgets/worklist_status_chip.dart';

class NurseWorklistScreen extends StatefulWidget {
  const NurseWorklistScreen({super.key});

  @override
  State<NurseWorklistScreen> createState() => _NurseWorklistScreenState();
}

class _NurseWorklistScreenState extends State<NurseWorklistScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<WorklistController>().load();
    });
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<WorklistController>();

    return PhoneWidth(
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Ward worklist'),
          actions: [
            IconButton(
              icon: const Icon(Icons.logout),
              tooltip: 'Sign out',
              onPressed: () => context.read<AuthController>().signOut(),
            ),
          ],
        ),
        body: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
              child: TextField(
                decoration: const InputDecoration(
                  hintText: 'Search by name or patient code',
                  prefixIcon: Icon(Icons.search),
                  border: OutlineInputBorder(),
                  isDense: true,
                ),
                textInputAction: TextInputAction.search,
                onSubmitted: controller.setSearch,
              ),
            ),
            SwitchListTile(
              dense: true,
              title: const Text('Show completed and cancelled'),
              value: controller.includeFinished,
              onChanged: controller.setIncludeFinished,
            ),
            const Divider(height: 1),
            Expanded(
              child: AsyncView<List<WorklistRow>>(
                state: controller.rows,
                onRetry: controller.load,
                builder: (context, rows) {
                  if (rows.isEmpty) {
                    return const EmptyView(
                      message: 'Nobody on the worklist right now.',
                    );
                  }
                  return RefreshIndicator(
                    onRefresh: controller.load,
                    child: ListView.separated(
                      itemCount: rows.length,
                      separatorBuilder: (_, __) => const Divider(height: 1),
                      itemBuilder: (_, index) =>
                          _WorklistTile(row: rows[index]),
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

class _WorklistTile extends StatelessWidget {
  const _WorklistTile({required this.row});

  final WorklistRow row;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final place = [row.wardName, row.bedNumber].whereType<String>().join(' · ');

    return ListTile(
      leading: const Icon(Icons.local_hospital_outlined),
      title: Text(row.patient.fullName),
      subtitle: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(row.patient.patientCode, style: theme.textTheme.bodySmall),
          if (place.isNotEmpty) Text(place, style: theme.textTheme.bodySmall),
        ],
      ),
      trailing: WorklistStatusChip(status: row.status),
      isThreeLine: place.isNotEmpty,
    );
  }
}
