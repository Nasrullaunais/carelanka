import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_view.dart';
import '../models/my_shift_item.dart';
import '../state/my_shifts_controller.dart';
import '../widgets/shift_card.dart';

class MyShiftsScreen extends StatelessWidget {
  const MyShiftsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<MyShiftsController>();
    final auth = context.read<AuthController>();

    // Show error snackbar if an action failed
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
        title: const Text('My Shifts'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            tooltip: 'Refresh',
            onPressed: () => controller.load(showLoading: false),
          ),
          IconButton(
            icon: const Icon(Icons.logout_rounded),
            tooltip: 'Sign out',
            onPressed: auth.signOut,
          ),
        ],
      ),
      body: AsyncView<List<MyShiftItem>>(
        state: controller.state,
        onRetry: controller.load,
        builder: (context, shifts) {
          if (shifts.isEmpty) {
            return RefreshableMessage(
              onRefresh: () => controller.load(showLoading: false),
              child: const EmptyView(
                icon: Icons.event_available_outlined,
                title: 'No shifts scheduled',
                message: 'You have no shifts assigned for the upcoming period. '
                    'Check with your ward administrator or duty manager.',
              ),
            );
          }

          return RefreshIndicator(
            onRefresh: () => controller.load(showLoading: false),
            child: ListView.separated(
              padding: const EdgeInsets.all(AppTheme.gutter),
              itemCount: shifts.length,
              separatorBuilder: (_, __) => const SizedBox(height: 12),
              itemBuilder: (context, index) {
                final shift = shifts[index];
                final isThisBusy = controller.busy &&
                    controller.actionAllocationId == shift.allocationId;

                return ShiftCard(
                  shift: shift,
                  isActionBusy: isThisBusy,
                  onClockIn: () async {
                    final success = await controller.clockIn(shift.allocationId);
                    if (success && context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(content: Text('Clocked in successfully.')),
                      );
                    }
                  },
                  onClockOut: () async {
                    final success = await controller.clockOut(shift.allocationId);
                    if (success && context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(content: Text('Clocked out successfully.')),
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
