import 'package:flutter/material.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/models/my_bill.dart';
import '../services/patient_service.dart';
import '../widgets/bill_view.dart';

/// Loaded when opened rather than with the list: a patient with twenty past visits
/// should not cost twenty requests to show a screen most of them never tap.
Future<void> showBillSheet(
  BuildContext context, {
  required PatientService service,
  required String admissionId,
}) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (_) => _BillSheet(service: service, admissionId: admissionId),
  );
}

class _BillSheet extends StatefulWidget {
  const _BillSheet({required this.service, required this.admissionId});

  final PatientService service;
  final String admissionId;

  @override
  State<_BillSheet> createState() => _BillSheetState();
}

class _BillSheetState extends State<_BillSheet> {
  MyBill? _bill;
  ApiException? _error;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final bill = await widget.service.loadMyBill(widget.admissionId);
      if (!mounted) return;
      setState(() {
        _bill = bill;
        _loading = false;
      });
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _error = error;
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 0, AppTheme.gutter, 24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Your bill', style: theme.textTheme.titleLarge),
            const SizedBox(height: 16),
            Flexible(child: SingleChildScrollView(child: _body())),
          ],
        ),
      ),
    );
  }

  Widget _body() {
    if (_loading) {
      return const Padding(
        padding: EdgeInsets.symmetric(vertical: 32),
        child: Center(child: CircularProgressIndicator()),
      );
    }

    final error = _error;

    if (error != null) {
      return error.code == PatientService.noBillCode || error.isNotFound
          ? const EmptyView(
              icon: Icons.receipt_long_outlined,
              title: 'No bill yet',
              message: 'The billing desk has not raised a bill for this stay.',
            )
          : Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Padding(
                  padding: const EdgeInsets.only(bottom: 16),
                  child: Text(error.message),
                ),
                OutlinedButton(onPressed: _load, child: const Text('Try again')),
              ],
            );
    }

    return BillView(bill: _bill!);
  }
}
