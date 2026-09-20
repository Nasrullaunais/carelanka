import 'package:carelanka_mobile/core/auth/auth_controller.dart';
import 'package:carelanka_mobile/core/auth/session_expiry.dart';
import 'package:carelanka_mobile/core/auth/token_store.dart';
import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/core/theme/app_theme.dart';
import 'package:carelanka_mobile/features/equipment/services/prescription_service.dart';
import 'package:carelanka_mobile/features/patient/screens/appointments_screen.dart';
import 'package:carelanka_mobile/features/patient/screens/home_screen.dart';
import 'package:carelanka_mobile/features/patient/screens/patient_shell.dart';
import 'package:carelanka_mobile/features/patient/screens/profile_screen.dart';
import 'package:carelanka_mobile/features/patient/state/appointments_controller.dart';
import 'package:carelanka_mobile/features/patient/state/my_stay_controller.dart';
import 'package:carelanka_mobile/features/patient/state/profile_controller.dart';
import 'package:carelanka_mobile/services/api_client/care_lanka_api.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import '../equipment/fake_prescriptions.dart';
import 'fake_patient_service.dart';

/// What a signed-in account with no hospital record behind it can see and do.
///
/// The whole patient area used to be replaced by the details form in this
/// state, which meant one unsaveable form and no way to sign out or switch
/// accounts. The tabs open now; the writes are what stay shut.
void main() {
  const notLinked =
      ApiException(message: 'not linked', statusCode: 404, code: 'cl_pat_033');
  const noAdmission =
      ApiException(message: 'not admitted', statusCode: 404, code: 'cl_pat_034');

  Future<void> pumpUnlinked(WidgetTester tester, Widget screen) async {
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final service = FakePatientService()
      ..profileResult = notLinked
      ..admissionResult = noAdmission
      ..appointmentsResult = appointmentPage([]);

    final profile = ProfileController(service);
    final stay = MyStayController(service);
    final appointments = AppointmentsController(service);

    await profile.load();
    await stay.load();
    await appointments.load();

    final sessionExpiry = SessionExpiry();
    addTearDown(sessionExpiry.dispose);
    final auth = AuthController(
      api: CareLankaApi(Dio()),
      tokens: TokenStore(),
      sessionExpiry: sessionExpiry,
    );
    addTearDown(auth.dispose);

    await tester.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider.value(value: profile),
          ChangeNotifierProvider.value(value: stay),
          ChangeNotifierProvider.value(value: appointments),
          ChangeNotifierProvider.value(value: auth),
          Provider<PrescriptionService>.value(value: FakePrescriptionService()),
        ],
        child: MaterialApp(theme: AppTheme.light, home: screen),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets('the shell still shows all five tabs', (tester) async {
    await pumpUnlinked(tester, const PatientShell());

    expect(find.byType(NavigationBar), findsOneWidget);
    for (final label in ['Home', 'Appointments', 'My stay', 'Prescriptions', 'Profile']) {
      expect(find.text(label), findsOneWidget, reason: '$label tab is missing');
    }
    expect(tester.takeException(), isNull);
  });

  testWidgets('home asks for the details instead of rendering blank', (tester) async {
    await pumpUnlinked(tester, HomeScreen(onOpenTab: (_) {}));

    expect(find.text('Complete your registration'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Add my details'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('appointments does not offer a booking the server would refuse', (tester) async {
    await pumpUnlinked(tester, const AppointmentsScreen());

    // The API answers an empty page rather than an error here, so the screen
    // cannot tell "no visits yet" from "no record" by the list alone.
    expect(find.byType(FloatingActionButton), findsNothing);
    expect(find.text('Book a visit'), findsNothing);
    expect(find.text('Complete your details'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('profile keeps sign out reachable', (tester) async {
    await pumpUnlinked(tester, const ProfileScreen());

    // The escape hatch. Signing up on the wrong account has to be undoable
    // without reinstalling the app.
    expect(find.text('Sign out'), findsOneWidget);
    expect(find.text('No hospital record'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
