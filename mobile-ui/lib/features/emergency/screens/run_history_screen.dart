import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/models/dispatch_summary.dart';
import '../models/run_step.dart';
import '../state/run_history_controller.dart';

class RunHistoryScreen extends StatefulWidget {
  const RunHistoryScreen({super.key});

  @override
  State<RunHistoryScreen> createState() => _RunHistoryScreenState();
}

class _RunHistoryScreenState extends State<RunHistoryScreen> {
  @override
  void initState() {
    super.initState();
    context.read<RunHistoryController>().load();
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
                  message: 'Runs you have finished, declined or that were cancelled will be listed here.',
                ),
              )
            : RefreshIndicator(
                onRefresh: controller.load,
                child: ListView.builder(
                  itemCount: history.items.length + (history.hasMore ? 1 : 0),
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

    return ListTile(
      title: Text('${run.ambulanceRegistration ?? 'Ambulance'} · ${run.status?.crewLabel ?? ''}'),
      subtitle: Text([
        if (dispatchedAt != null) FriendlyDate.full(dispatchedAt),
        if (crew != null) '$crew crew on board',
        if (run.callPriority != null) 'Priority ${run.callPriority!.name}',
      ].join(' · ')),
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
                  TextButton(onPressed: controller.loadMore, child: Text(error == null ? 'Show older runs' : 'Try again')),
                ],
              ),
      ),
    );
  }
}
