import 'package:carelanka_mobile/core/theme/app_theme.dart';
import 'package:carelanka_mobile/features/equipment/state/equipment_confirmation_controller.dart';
import 'package:carelanka_mobile/features/equipment/widgets/confirmation_code_gate.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'fake_confirmations.dart';

void main() {
  // WIDGET + FORM-VALIDATION testing: unlike the web version, the Unlock button here
  // (confirmation_code_gate.dart line 67) is never disabled for a blank code - it is
  // only disabled while a request is already in flight. So blank-code validation must
  // show up as the TextField's errorText after a real, failed attempt, not as a
  // disabled button. Nothing in the existing suite renders ConfirmationCodeGate on
  // its own to check that wiring.
  //
  // DEFECT FOUND IN OUR OWN TEST DESIGN (not the app): the first version of this test
  // rendered ConfirmationCodeGate directly under a plain Scaffold and failed, because
  // the widget itself never listens to `controller` (no context.watch/Consumer inside
  // it) - it only reads controller.unlockProblem at build time, trusting an ancestor
  // to rebuild it when the controller calls notifyListeners(). Fixed by wrapping it in
  // an AnimatedBuilder, which any ChangeNotifier works with directly as a Listenable.
  testWidgets('shows "Enter the confirmation code." on the field after tapping Unlock blank', (
    tester,
  ) async {
    final controller = EquipmentConfirmationController(FakeEquipmentConfirmationService());

    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.light,
        home: Scaffold(
          body: AnimatedBuilder(
            animation: controller,
            builder: (context, _) =>
                ConfirmationCodeGate(controller: controller, explanation: 'Test explanation.'),
          ),
        ),
      ),
    );

    expect(find.text('Enter the confirmation code'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, 'Unlock'));
    await tester.pumpAndSettle();

    expect(find.text('Enter the confirmation code.'), findsOneWidget);
  });
}
