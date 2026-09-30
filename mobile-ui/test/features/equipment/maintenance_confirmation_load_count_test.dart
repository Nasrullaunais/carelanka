import 'package:carelanka_mobile/core/widgets/async_data.dart';
import 'package:carelanka_mobile/features/equipment/state/maintenance_confirmation_controller.dart';
import 'package:flutter_test/flutter_test.dart';

import 'fake_confirmations.dart';

void main() {
  // API-INTEGRATION testing: loadCount() (confirmation_queue_controller.dart, lines
  // 40-50) is the boundary between the maintenance-confirmation SERVICE and the
  // controller/UI - this proves a count returned by the fake "API" actually flows
  // through untouched into awaitingCount. Nothing in the existing suite calls
  // loadCount() at all; it only exercises the code-gated list/confirm endpoints.
  test('loadCount() reflects exactly what the service reports as awaiting', () async {
    final service = FakeMaintenanceConfirmationService(
      jobs: [openJob(), openJob(id: 'job-2', label: 'Monitor (asset tag EQ-0102)')],
    );
    final controller = MaintenanceConfirmationController(service);

    expect(controller.awaitingCount, isA<AsyncLoading<int>>());

    await controller.loadCount();

    expect(controller.awaitingCount, isA<AsyncReady<int>>());
    expect(controller.awaitingCount.valueOrNull, 2);
  });
}
