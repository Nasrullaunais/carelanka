import 'package:carelanka_mobile/core/auth/auth_controller.dart';
import 'package:carelanka_mobile/core/auth/patient_login_screen.dart';
import 'package:carelanka_mobile/core/auth/session_expiry.dart';
import 'package:carelanka_mobile/core/auth/token_store.dart';
import 'package:carelanka_mobile/core/config/hospital_contact.dart';
import 'package:carelanka_mobile/core/routing/app_router.dart';
import 'package:carelanka_mobile/features/patient/screens/manage_account_screen.dart';
import 'package:carelanka_mobile/services/api_client/care_lanka_api.dart';
import 'package:carelanka_mobile/services/api_client/models/current_principal.dart';
import 'package:carelanka_mobile/services/api_client/models/principal_role.dart';
import 'package:carelanka_mobile/services/api_client/models/principal_type.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

class _FakeAuth extends AuthController {
  _FakeAuth(SessionExpiry sessionExpiry)
      : super(api: CareLankaApi(Dio()), tokens: TokenStore(), sessionExpiry: sessionExpiry);

  AuthStatus fakeStatus = AuthStatus.signedOut;
  CurrentPrincipal? fakePrincipal;

  @override
  AuthStatus get status => fakeStatus;

  @override
  CurrentPrincipal? get principal => fakePrincipal;

  void signInAsPatientWith({required bool mustChangePassword}) {
    fakeStatus = AuthStatus.signedIn;
    fakePrincipal = CurrentPrincipal(
      id: 'account-1',
      principalType: PrincipalType.patient,
      role: PrincipalRole.patient,
      displayName: 'kamala.p',
      mustChangePassword: mustChangePassword,
    );
    notifyListeners();
  }
}

void main() {
  late _FakeAuth auth;

  Future<GoRouter> pumpApp(WidgetTester tester, {required bool mustChangePassword}) async {
    final sessionExpiry = SessionExpiry();
    addTearDown(sessionExpiry.dispose);
    auth = _FakeAuth(sessionExpiry);
    addTearDown(auth.dispose);

    final router = createAppRouter(
      auth: auth,
      routes: [
        GoRoute(path: '/me', builder: (_, __) => const Scaffold(body: Text('patient area'))),
      ],
      homePathFor: (_) => '/me',
    );
    addTearDown(router.dispose);

    await tester.pumpWidget(ChangeNotifierProvider<AuthController>.value(
      value: auth,
      child: MaterialApp.router(routerConfig: router),
    ));
    auth.signInAsPatientWith(mustChangePassword: mustChangePassword);
    await tester.pumpAndSettle();

    return router;
  }

  testWidgets('a patient signed in with a temporary password can only choose a new one',
      (tester) async {
    final router = await pumpApp(tester, mustChangePassword: true);

    expect(find.text('Choose a new password'), findsOneWidget);
    expect(find.text('patient area'), findsNothing);

    router.go('/me');
    await tester.pumpAndSettle();

    expect(find.text('Choose a new password'), findsOneWidget);
    expect(find.text('patient area'), findsNothing);
  });

  testWidgets('the forced screen has no way back, only sign out', (tester) async {
    await pumpApp(tester, mustChangePassword: true);

    expect(find.byType(BackButton), findsNothing);
    expect(find.widgetWithText(TextButton, 'Sign out'), findsOneWidget);
    expect(
      find.textContaining('The hospital gave you a temporary password'),
      findsOneWidget,
    );
  });

  testWidgets('without the flag the change-password address goes home', (tester) async {
    final router = await pumpApp(tester, mustChangePassword: false);

    router.go(AppRoutes.changePassword);
    await tester.pumpAndSettle();

    expect(find.text('patient area'), findsOneWidget);
    expect(find.text('Choose a new password'), findsNothing);
  });

  testWidgets('Forgot password on sign-in shows how to reach the hospital', (tester) async {
    await tester.pumpWidget(const MaterialApp(home: PatientLoginScreen()));

    await tester.tap(find.widgetWithText(TextButton, 'Forgot password?'));
    await tester.pumpAndSettle();

    expect(find.text('Reset your password'), findsOneWidget);
    expect(find.text(HospitalContact.reception), findsOneWidget);
    expect(find.text(HospitalContact.email), findsOneWidget);
  });

  testWidgets('Reset password in Manage account shows the same help', (tester) async {
    final sessionExpiry = SessionExpiry();
    addTearDown(sessionExpiry.dispose);
    auth = _FakeAuth(sessionExpiry);
    addTearDown(auth.dispose);
    auth.signInAsPatientWith(mustChangePassword: false);

    await tester.pumpWidget(ChangeNotifierProvider<AuthController>.value(
      value: auth,
      child: const MaterialApp(home: ManageAccountScreen()),
    ));

    await tester.tap(find.text('Reset password'));
    await tester.pumpAndSettle();

    expect(find.text('Reset your password'), findsOneWidget);
    expect(find.text(HospitalContact.reception), findsOneWidget);
  });
}
