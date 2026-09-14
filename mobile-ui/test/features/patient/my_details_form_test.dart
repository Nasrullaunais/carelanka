import 'package:carelanka_mobile/core/auth/auth_controller.dart';
import 'package:carelanka_mobile/core/auth/session_expiry.dart';
import 'package:carelanka_mobile/core/auth/token_store.dart';
import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/core/theme/app_theme.dart';
import 'package:carelanka_mobile/features/patient/screens/my_details_screen.dart';
import 'package:carelanka_mobile/features/patient/state/profile_controller.dart';
import 'package:carelanka_mobile/services/api_client/care_lanka_api.dart';
import 'package:carelanka_mobile/services/api_client/models/gender.dart';
import 'package:carelanka_mobile/services/api_client/models/my_profile.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'fake_patient_service.dart';

/// The record the save comes back with. The form never reads it, but the
/// controller needs something to hold once the request succeeds.
const _savedProfile = MyProfile(
  patientCode: 'PQ2957CP',
  fullName: 'Chathura Wijesinghe',
  nic: '199012345678',
  gender: Gender.male,
  detailsComplete: false,
  missingFields: ['address'],
);

void main() {
  /// A brand-new login: the account exists, nothing is linked behind it, so the
  /// shell shows this form full-screen.
  Future<FakePatientService> pumpFirstRunForm(WidgetTester tester) async {
    // Tall enough that the whole form fits without scrolling. Whether it fits
    // a real phone is the layout test's job; this file is about validation.
    tester.view.physicalSize = const Size(390, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final service = FakePatientService()
      ..profileResult =
          const ApiException(message: 'not linked', statusCode: 404, code: 'cl_pat_033')
      ..savedProfileResult = _savedProfile;

    final profile = ProfileController(service);
    await profile.load();

    final auth = AuthController(
      api: CareLankaApi(Dio()),
      tokens: TokenStore(),
      sessionExpiry: SessionExpiry(),
    );
    addTearDown(auth.dispose);

    await tester.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider.value(value: profile),
          ChangeNotifierProvider.value(value: auth),
        ],
        child: MaterialApp(
          theme: AppTheme.light,
          home: const MyDetailsScreen(firstTime: true),
        ),
      ),
    );
    await tester.pump();

    return service;
  }

  Future<void> fillTextFields(WidgetTester tester) async {
    await tester.enterText(find.widgetWithText(TextFormField, 'Full name'), 'Chathura');
    await tester.enterText(find.widgetWithText(TextFormField, 'NIC'), '199012345678');
    await tester.enterText(find.widgetWithText(TextFormField, 'Phone'), '0771234567');
  }

  Future<void> tapContinue(WidgetTester tester) async {
    await tester.tap(find.widgetWithText(FilledButton, 'Continue'));
    await tester.pumpAndSettle();
  }

  Future<void> chooseGender(WidgetTester tester, String label) async {
    await tester.tap(find.byType(DropdownButtonFormField<Gender>));
    await tester.pumpAndSettle();
    await tester.tap(find.text(label).last);
    await tester.pumpAndSettle();
  }

  Future<void> pickDateOfBirth(WidgetTester tester) async {
    await tester.tap(find.widgetWithText(InkWell, 'Date of birth').first);
    await tester.pumpAndSettle();
    await tester.tap(find.text('OK'));
    await tester.pumpAndSettle();
  }

  testWidgets('nothing is sent until gender and date of birth are chosen', (tester) async {
    final service = await pumpFirstRunForm(tester);

    await fillTextFields(tester);
    await tapContinue(tester);

    expect(service.savedDetails, isNull);
    expect(find.text('Choose your gender'), findsOneWidget);
    expect(find.text('Enter your date of birth'), findsOneWidget);
  });

  testWidgets('an empty contact number blocks the save too', (tester) async {
    final service = await pumpFirstRunForm(tester);

    await tester.enterText(find.widgetWithText(TextFormField, 'Full name'), 'Chathura');
    await tester.enterText(find.widgetWithText(TextFormField, 'NIC'), '199012345678');
    await tapContinue(tester);

    expect(service.savedDetails, isNull);
    expect(find.text('Enter your phone number'), findsOneWidget);
  });

  testWidgets('address and emergency contact are not required', (tester) async {
    final service = await pumpFirstRunForm(tester);

    await fillTextFields(tester);

    await chooseGender(tester, 'Female');
    await pickDateOfBirth(tester);
    await tapContinue(tester);

    expect(service.savedDetails, isNotNull);
    expect(service.savedDetails!.gender, Gender.female);
    expect(service.savedDetails!.dateOfBirth, isNotNull);
    expect(service.savedDetails!.address, isNull);
    expect(service.savedDetails!.emergencyContactName, isNull);
  });

  testWidgets('the gender picker offers male and female only', (tester) async {
    await pumpFirstRunForm(tester);

    await tester.tap(find.byType(DropdownButtonFormField<Gender>));
    await tester.pumpAndSettle();

    expect(find.text('Male'), findsWidgets);
    expect(find.text('Female'), findsWidgets);
    expect(find.text('Other'), findsNothing);
    expect(find.text('Prefer not to say'), findsNothing);
  });

  testWidgets('the first run offers a way back out to sign-in', (tester) async {
    await pumpFirstRunForm(tester);

    // Nothing sits behind this screen in the navigator, so without this the
    // only way off a half-finished sign-up is to reinstall the app.
    expect(find.widgetWithText(TextButton, 'Sign out'), findsOneWidget);
  });
}
