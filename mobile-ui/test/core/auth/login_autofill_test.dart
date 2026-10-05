import 'package:carelanka_mobile/core/auth/auth_controller.dart';
import 'package:carelanka_mobile/core/auth/patient_login_screen.dart';
import 'package:carelanka_mobile/core/auth/session_expiry.dart';
import 'package:carelanka_mobile/core/auth/staff_login_screen.dart';
import 'package:carelanka_mobile/core/auth/token_store.dart';
import 'package:carelanka_mobile/services/api_client/care_lanka_api.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

/// The phone's password manager offers to save a login only when the app says the
/// login is finished, so that must happen after a success and never after a rejection.
void main() {
  Future<void> pumpScreen(WidgetTester tester, Widget screen, {required bool accept}) async {
    final sessionExpiry = SessionExpiry();
    addTearDown(sessionExpiry.dispose);
    final auth = _FixedAnswerAuth(accept: accept, sessionExpiry: sessionExpiry);
    addTearDown(auth.dispose);

    await tester.pumpWidget(
      ChangeNotifierProvider<AuthController>.value(
        value: auth,
        child: MaterialApp(home: screen),
      ),
    );
  }

  Future<void> signIn(WidgetTester tester, String user) async {
    await tester.enterText(find.byType(TextFormField).at(0), user);
    await tester.enterText(find.byType(TextFormField).at(1), 'CareLanka#2026');
    await tester.tap(find.text('Sign in').last);
    await tester.pumpAndSettle();
  }

  bool savedLogin(WidgetTester tester) => tester.testTextInput.log.any(
    (call) => call.method == 'TextInput.finishAutofillContext' && call.arguments == true,
  );

  List<Iterable<String>?> hints(WidgetTester tester) => tester
      .widgetList<TextField>(find.byType(TextField))
      .map((field) => field.autofillHints)
      .toList();

  testWidgets('staff sign-in labels its fields for the password manager', (tester) async {
    await pumpScreen(tester, const StaffLoginScreen(), accept: true);

    expect(find.byType(AutofillGroup), findsOneWidget);
    expect(hints(tester), [
      [AutofillHints.username, AutofillHints.email],
      [AutofillHints.password],
    ]);
  });

  testWidgets('an accepted staff login is offered for saving', (tester) async {
    await pumpScreen(tester, const StaffLoginScreen(), accept: true);
    await signIn(tester, 'crew.fernando@carelanka.lk');

    expect(savedLogin(tester), isTrue);
  });

  testWidgets('a rejected staff login is not offered for saving', (tester) async {
    await pumpScreen(tester, const StaffLoginScreen(), accept: false);
    await signIn(tester, 'crew.fernando@carelanka.lk');

    expect(savedLogin(tester), isFalse);
  });

  testWidgets('patient sign-in labels its fields and saves only an accepted login', (tester) async {
    await pumpScreen(tester, const PatientLoginScreen(), accept: true);

    expect(hints(tester), [
      [AutofillHints.username],
      [AutofillHints.password],
    ]);
    await signIn(tester, 'nimal');
    expect(savedLogin(tester), isTrue);
  });

  testWidgets('a rejected patient login is not offered for saving', (tester) async {
    await pumpScreen(tester, const PatientLoginScreen(), accept: false);
    await signIn(tester, 'nimal');

    expect(savedLogin(tester), isFalse);
  });
}

class _FixedAnswerAuth extends AuthController {
  _FixedAnswerAuth({required this.accept, required super.sessionExpiry})
    : super(api: CareLankaApi(Dio()), tokens: TokenStore());

  final bool accept;

  @override
  Future<bool> signInAsStaff({required String email, required String password}) async => accept;

  @override
  Future<bool> signInAsPatient({required String username, required String password}) async =>
      accept;
}
