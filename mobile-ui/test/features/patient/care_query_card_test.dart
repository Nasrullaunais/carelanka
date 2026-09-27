import 'package:carelanka_mobile/core/theme/app_theme.dart';
import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/features/patient/widgets/care_query_card.dart';
import 'package:carelanka_mobile/features/patient/services/patient_service.dart';
import 'package:carelanka_mobile/services/api_client/models/admission_status.dart';
import 'package:carelanka_mobile/services/api_client/models/my_admission.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'fake_patient_service.dart';

void main() {
  const admissionA = MyAdmission(
    admissionId: 'a1',
    status: AdmissionStatus.admitted,
    statusText: 'Admitted',
    detailsComplete: true,
    missingFields: [],
  );
  const admissionB = MyAdmission(
    admissionId: 'a2',
    status: AdmissionStatus.admitted,
    statusText: 'Admitted',
    detailsComplete: true,
    missingFields: [],
  );

  testWidgets('reloads history when the admission object changes', (
    tester,
  ) async {
    final service = FakePatientService();

    await tester.pumpWidget(
      Provider<PatientService>.value(
        value: service,
        child: MaterialApp(
          theme: AppTheme.light,
          home: const CareQueryCard(admission: admissionA),
        ),
      ),
    );
    await tester.pump();
    expect(service.careRecommendationLoadCalls, 1);

    await tester.pumpWidget(
      Provider<PatientService>.value(
        value: service,
        child: MaterialApp(
          theme: AppTheme.light,
          home: const CareQueryCard(admission: admissionB),
        ),
      ),
    );
    await tester.pump();

    expect(service.careRecommendationLoadCalls, 2);
  });

  testWidgets(
    'blocks sending and shows a countdown after the rate limit error',
    (tester) async {
      final service = FakePatientService()
        ..submitCareQueryResult = const ApiException(
          message: 'Too many messages.',
          statusCode: 429,
          code: 'cl_pat_050',
        );

      await tester.pumpWidget(
        Provider<PatientService>.value(
          value: service,
          child: MaterialApp(
            theme: AppTheme.light,
            home: const CareQueryCard(admission: admissionA),
          ),
        ),
      );
      await tester.pump();
      await tester.enterText(find.byType(TextFormField), 'I need help.');
      await tester.tap(find.text('Send'));
      await tester.pump();

      final send = tester.widget<FilledButton>(
        find.widgetWithText(FilledButton, 'Send'),
      );
      expect(send.onPressed, isNull);
      expect(
        find.textContaining('You have already sent 3 messages'),
        findsOneWidget,
      );
    },
  );
}
