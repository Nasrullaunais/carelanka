import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_view.dart';
import '../../../core/widgets/phone_width.dart';
import '../../../services/api_client/models/equipment_item.dart';
import '../state/equipment_confirmation_controller.dart';

class EquipmentConfirmationScreen extends StatelessWidget {
  const EquipmentConfirmationScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<EquipmentConfirmationController>();

    return PhoneWidth(
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Equipment confirmation'),
          actions: [
            if (controller.isUnlocked)
              IconButton(
                icon: const Icon(Icons.lock_outline),
                tooltip: 'Lock',
                onPressed: controller.lock,
              ),
          ],
        ),
        body: controller.isUnlocked ? const _AwaitingList() : const _CodeGate(),
      ),
    );
  }
}

class _CodeGate extends StatefulWidget {
  const _CodeGate();

  @override
  State<_CodeGate> createState() => _CodeGateState();
}

class _CodeGateState extends State<_CodeGate> {
  final _code = TextEditingController();

  @override
  void dispose() {
    _code.dispose();
    super.dispose();
  }

  Future<void> _unlock() async {
    final unlocked = await context.read<EquipmentConfirmationController>().unlock(_code.text);
    if (!unlocked) _code.clear();
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<EquipmentConfirmationController>();
    final problem = controller.unlockProblem;
    final theme = Theme.of(context);

    return ListView(
      padding: const EdgeInsets.all(AppTheme.gutter),
      children: [
        Icon(Icons.lock_outline, size: 40, color: theme.colorScheme.primary),
        const SizedBox(height: 12),
        Text(
          'Enter the confirmation code',
          style: theme.textTheme.titleMedium,
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 6),
        Text(
          'New equipment only reaches the web dashboard after you confirm it here.',
          style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: AppTheme.gutter),
        TextField(
          controller: _code,
          enabled: !controller.unlocking,
          obscureText: true,
          autocorrect: false,
          enableSuggestions: false,
          decoration: InputDecoration(
            labelText: 'Confirmation code',
            errorText: problem,
          ),
          textInputAction: TextInputAction.done,
          onSubmitted: (_) => _unlock(),
        ),
        const SizedBox(height: AppTheme.gutter),
        FilledButton.icon(
          onPressed: controller.unlocking ? null : _unlock,
          icon: controller.unlocking
              ? const SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Icon(Icons.lock_open),
          label: Text(controller.unlocking ? 'Checking…' : 'Unlock'),
          style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(52)),
        ),
      ],
    );
  }
}

class _AwaitingList extends StatelessWidget {
  const _AwaitingList();

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<EquipmentConfirmationController>();

    return AsyncView<List<EquipmentItem>>(
      state: controller.items,
      onRetry: controller.reload,
      loading: const Padding(
        padding: EdgeInsets.all(AppTheme.gutter),
        child: Column(
          children: [
            Skeleton(height: 150),
            SizedBox(height: 10),
            Skeleton(height: 150),
          ],
        ),
      ),
      builder: (context, items) {
        if (items.isEmpty) {
          return RefreshableMessage(
            onRefresh: controller.reload,
            child: const EmptyView(
              icon: Icons.fact_check_outlined,
              title: 'Nothing to confirm',
              message: 'New items the equipment manager registers appear here.',
            ),
          );
        }

        return RefreshIndicator(
          onRefresh: controller.reload,
          child: ListView.separated(
            padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 12, AppTheme.gutter, 32),
            itemCount: items.length,
            separatorBuilder: (_, __) => const SizedBox(height: 10),
            itemBuilder: (_, index) => _AwaitingItemCard(item: items[index]),
          ),
        );
      },
    );
  }
}

class _AwaitingItemCard extends StatelessWidget {
  const _AwaitingItemCard({required this.item});

  final EquipmentItem item;

  Future<void> _confirm(BuildContext context) async {
    final controller = context.read<EquipmentConfirmationController>();
    final messenger = ScaffoldMessenger.of(context);

    final refused = await controller.confirm(item);

    messenger.showSnackBar(SnackBar(
      content: Text(refused?.message ?? '${item.name} confirmed. It now shows on the web dashboard.'),
    ));
  }

  Future<void> _reject(BuildContext context) async {
    final controller = context.read<EquipmentConfirmationController>();
    final messenger = ScaffoldMessenger.of(context);

    final sure = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text('Reject ${item.name}?'),
        content: Text(
          'It is removed and never reaches the dashboard. The equipment manager can register '
          '${item.assetTag} again with the right details.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('Reject'),
          ),
        ],
      ),
    );
    if (sure != true) return;

    final refused = await controller.reject(item);

    messenger.showSnackBar(SnackBar(
      content: Text(refused?.message ?? '${item.name} rejected.'),
    ));
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<EquipmentConfirmationController>();
    final busy = controller.isBusy(item.id);
    final theme = Theme.of(context);
    final serial = item.serialNumber;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(item.name, style: theme.textTheme.titleMedium),
            const SizedBox(height: 2),
            Text(
              '${item.assetTag} · ${item.categoryName}',
              style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 10),
            _Detail(label: 'Make', value: '${item.manufacturer} ${item.model}'),
            if (serial != null) _Detail(label: 'Serial', value: serial),
            _Detail(label: 'Location', value: item.wardName ?? 'Central store'),
            _Detail(label: 'Purchased', value: FriendlyDate.date(item.purchaseDate)),
            _Detail(label: 'Registered', value: FriendlyDate.full(item.createdAt)),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: busy ? null : () => _reject(context),
                    style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(48)),
                    child: const Text('Reject'),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: FilledButton(
                    onPressed: busy ? null : () => _confirm(context),
                    style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
                    child: busy
                        ? const SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Text('Confirm'),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _Detail extends StatelessWidget {
  const _Detail({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 92,
            child: Text(
              label,
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          ),
          Expanded(child: Text(value, style: theme.textTheme.bodyMedium)),
        ],
      ),
    );
  }
}
