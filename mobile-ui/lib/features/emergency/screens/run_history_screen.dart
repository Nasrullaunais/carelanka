import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/models/dispatch_summary.dart';
import '../emergency_routes.dart';
import '../models/run_step.dart';
import '../state/run_history_controller.dart';
import '../widgets/priority_pill.dart';

class RunHistoryScreen extends StatefulWidget {
  const RunHistoryScreen({super.key});

  @override
  State<RunHistoryScreen> createState() => _RunHistoryScreenState();
}

class _RunHistoryScreenState extends State<RunHistoryScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.read<RunHistoryController>().load();
    });
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<RunHistoryController>();

    return Scaffold(
      appBar: AppBar(title: const Text('Past runs')),
      body: AsyncView(
        state: controller.state,
        onRetry: controller.load,
        builder: (context, history) => history.items.isEmpty
            ? RefreshableMessage(
                onRefresh: controller.load,
                child: const EmptyView(
                  icon: Icons.history,
                  title: 'No past runs',
                  message:
                      'Runs you have finished, declined or that were cancelled will be listed here.',
                ),
              )
            : RefreshIndicator(
                onRefresh: controller.load,
                child: ListView.separated(
                  padding: EdgeInsets.fromLTRB(
                    AppTheme.gutter,
                    8,
                    AppTheme.gutter,
                    AppTheme.gutter + MediaQuery.paddingOf(context).bottom,
                  ),
                  itemCount: history.items.length + (history.hasMore ? 1 : 0),
                  separatorBuilder: (_, _) => const SizedBox(height: 12),
                  itemBuilder: (context, index) => index < history.items.length
                      ? _RunTile(run: history.items[index])
                      : _LoadMore(controller: controller),
                ),
              ),
      ),
    );
  }
}

class _RunTile extends StatelessWidget {
  const _RunTile({required this.run});

  final DispatchSummary run;

  @override
  Widget build(BuildContext context) {
    final dispatchedAt = run.dispatchedAt;
    final crew = run.crewCount;

    final id = run.id;

    final theme = Theme.of(context);

    return Card(
      child: InkWell(
        onTap: id == null
            ? null
            : () => context.push('${EmergencyPaths.history}/$id'),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 16, 12, 16),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      run.status?.crewLabel ?? '',
                      style: theme.textTheme.titleMedium,
                    ),
                    const SizedBox(height: 4),
                    Text(
                      [
                        run.ambulanceRegistration ?? 'Ambulance',
                        if (crew != null) '$crew crew on board',
                      ].join(' · '),
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: theme.colorScheme.onSurfaceVariant,
                      ),
                    ),
                    const SizedBox(height: 10),
                    Wrap(
                      spacing: 10,
                      runSpacing: 6,
                      crossAxisAlignment: WrapCrossAlignment.center,
                      children: [
                        if (run.callPriority != null)
                          PriorityPill(priority: run.callPriority),
                        if (dispatchedAt != null)
                          Text(
                            FriendlyDate.full(dispatchedAt),
                            style: theme.textTheme.bodySmall?.copyWith(
                              color: theme.colorScheme.onSurfaceVariant,
                            ),
                          ),
                      ],
                    ),
                  ],
                ),
              ),
              Icon(
                Icons.chevron_right,
                color: theme.colorScheme.onSurfaceVariant,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _LoadMore extends StatelessWidget {
  const _LoadMore({required this.controller});

  final RunHistoryController controller;

  @override
  Widget build(BuildContext context) {
    final error = controller.moreError;

    return Padding(
      padding: const EdgeInsets.all(16),
      child: Center(
        child: controller.loadingMore
            ? const CircularProgressIndicator()
            : Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  if (error != null) Text(error.message),
                  TextButton(
                    onPressed: controller.loadMore,
                    child: Text(
                      error == null ? 'Show older runs' : 'Try again',
                    ),
                  ),
                ],
              ),
      ),
    );
  }
}
