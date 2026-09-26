import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_view.dart';
import '../models/staff_leave_item.dart';
import '../state/leave_requests_controller.dart';
import '../widgets/leave_request_card.dart';
import '../widgets/request_leave_dialog.dart';

class LeaveRequestsScreen extends StatelessWidget {
  const LeaveRequestsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<LeaveRequestsController>();

    if (controller.actionError != null) {
      final errorMessage = controller.actionError!.message;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(errorMessage),
            backgroundColor: Theme.of(context).colorScheme.error,
          ),
        );
        controller.clearError();
      });
    }

    return Scaffold(
      appBar: AppBar(
        title: const Text('My Leave Requests'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            tooltip: 'Refresh',
            onPressed: () => controller.load(showLoading: false),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () async {
          final submitted = await showRequestLeaveDialog(context, controller);
          if (submitted == true && context.mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(content: Text('Leave request submitted successfully.')),
            );
          }
        },
        icon: const Icon(Icons.add_rounded),
        label: const Text('Request Leave'),
      ),
      body: AsyncView<List<StaffLeaveItem>>(
        state: controller.state,
        onRetry: controller.load,
        builder: (context, requests) {
          if (requests.isEmpty) {
            return RefreshableMessage(
              onRefresh: () => controller.load(showLoading: false),
              child: EmptyView(
                icon: Icons.beach_access_outlined,
                title: 'No leave requests',
                message: 'You have not submitted any leave requests yet. '
                    'Tap Request Leave to apply for scheduled time off.',
                action: FilledButton.icon(
                  onPressed: () async {
                    final submitted = await showRequestLeaveDialog(context, controller);
                    if (submitted == true && context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(content: Text('Leave request submitted successfully.')),
                      );
                    }
                  },
                  icon: const Icon(Icons.add_rounded),
                  label: const Text('Request Leave'),
                ),
              ),
            );
          }

          return RefreshIndicator(
            onRefresh: () => controller.load(showLoading: false),
            child: ListView.separated(
              padding: const EdgeInsets.fromLTRB(
                AppTheme.gutter,
                AppTheme.gutter,
                AppTheme.gutter,
                88, // bottom padding for FAB
              ),
              itemCount: requests.length,
              separatorBuilder: (_, __) => const SizedBox(height: 12),
              itemBuilder: (context, index) {
                final item = requests[index];
                final isThisBusy = controller.busy && controller.actionRequestId == item.id;

                return LeaveRequestCard(
                  item: item,
                  isActionBusy: isThisBusy,
                  onWithdraw: () async {
                    final success = await controller.withdrawRequest(item.id);
                    if (success && context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(content: Text('Leave request withdrawn.')),
                      );
                    }
                  },
                );
              },
            ),
          );
        },
      ),
    );
  }
}
