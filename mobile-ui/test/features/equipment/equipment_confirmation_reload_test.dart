import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/features/equipment/services/equipment_confirmation_service.dart';
import 'package:carelanka_mobile/features/equipment/state/equipment_confirmation_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/equipment_item.dart';
import 'package:flutter_test/flutter_test.dart';

import 'fake_confirmations.dart';

// Succeeds on the first call (the initial unlock), then fails with a 403 on every
// call after that - simulating an administrator's confirmation code being reset by
// someone else while this screen is still open.
class _CodeRevokedMidSessionService implements EquipmentConfirmationService {
  var _calls = 0;

  @override
  Future<int> countAwaiting() async => 0;

  @override
  Future<List<EquipmentItem>> listAwaiting(String code) async {
    _calls++;
    if (_calls == 1) return [pendingItem()];
    throw const ApiException(
      message: 'Your confirmation code was reset. Enter it again.',
      statusCode: 403,
    );
  }

  @override
  Future<EquipmentItem> confirm(String id, String code) async => throw UnimplementedError();

  @override
  Future<void> reject(String id, String code) async => throw UnimplementedError();
}

void main() {
  test(
    'reload() locks the queue and shows the server message when the code is revoked mid-session',
    () async {
      // ConfirmationQueueController.reload() (confirmation_queue_controller.dart,
      // lines 91-94): on a 403 from fetchAwaiting, it must call lock(problem: ...)
      // rather than just showing a failed list - so the administrator is asked to
      // re-enter the code, not left staring at a stuck error screen. Nothing in the
      // existing test suite exercises reload() at all.
      final controller = EquipmentConfirmationController(_CodeRevokedMidSessionService());

      final unlocked = await controller.unlock(confirmationCode);
      expect(unlocked, isTrue);

      await controller.reload();

      expect(controller.isUnlocked, isFalse);
      expect(controller.unlockProblem, 'Your confirmation code was reset. Enter it again.');
    },
  );
}
