import 'package:carelanka_mobile/core/auth/auth_form.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  late TextEditingController password;
  late TextEditingController confirm;

  setUp(() {
    password = TextEditingController(text: 'Secret123');
    confirm = TextEditingController(text: 'Secret123');
  });

  tearDown(() {
    password.dispose();
    confirm.dispose();
  });

  Future<void> pumpForm(WidgetTester tester) => tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: Column(
              children: [
                AuthPasswordField(controller: password, label: 'Password', enabled: true),
                AuthPasswordField(controller: confirm, label: 'Confirm password', enabled: true),
              ],
            ),
          ),
        ),
      );

  bool isObscured(WidgetTester tester, String label) => tester
      .widget<TextField>(find.descendant(
        of: find.widgetWithText(AuthPasswordField, label),
        matching: find.byType(TextField),
      ))
      .obscureText;

  Finder eyeIn(String label) => find.descendant(
        of: find.widgetWithText(AuthPasswordField, label),
        matching: find.byType(IconButton),
      );

  testWidgets('no eye button shows until a box is tapped', (tester) async {
    await pumpForm(tester);

    expect(find.byType(IconButton), findsNothing);
  });

  testWidgets('the eye shows only on the box being typed in', (tester) async {
    await pumpForm(tester);

    await tester.tap(find.widgetWithText(TextField, 'Password'));
    await tester.pump();

    expect(eyeIn('Password'), findsOneWidget);
    expect(eyeIn('Confirm password'), findsNothing);
  });

  testWidgets('revealing one box leaves the other hidden', (tester) async {
    await pumpForm(tester);

    await tester.tap(find.widgetWithText(TextField, 'Password'));
    await tester.pump();
    await tester.tap(eyeIn('Password'));
    await tester.pump();

    expect(isObscured(tester, 'Password'), isFalse);
    expect(isObscured(tester, 'Confirm password'), isTrue);
  });

  testWidgets('moving to the other box hides the first again', (tester) async {
    await pumpForm(tester);

    await tester.tap(find.widgetWithText(TextField, 'Password'));
    await tester.pump();
    await tester.tap(eyeIn('Password'));
    await tester.pump();

    await tester.tap(find.widgetWithText(TextField, 'Confirm password'));
    await tester.pump();

    expect(isObscured(tester, 'Password'), isTrue);
    expect(eyeIn('Password'), findsNothing);
    expect(eyeIn('Confirm password'), findsOneWidget);
  });
}
