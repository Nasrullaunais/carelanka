import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/core/widgets/async_data.dart';
import 'package:carelanka_mobile/features/patient/state/my_stay_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/admission_status.dart';
import 'package:carelanka_mobile/services/api_client/models/my_admission.dart';
import 'package:flutter_test/flutter_test.dart';

import 'fake_patient_service.dart';

MyStay _stayFrom(AsyncData<MyStay> state) => (state as AsyncReady<MyStay>).value;

Future<MyStay?> _loadWith(Object result) async {
  final service = FakePatientService()..admissionResult = result;
  final controller = MyStayController(service);
  await controller.load();
  return controller.state is AsyncReady<MyStay> ? _stayFrom(controller.state) : null;
}

void main() {
  // Both codes come back as 404 from /me/admission and mean different things.
  // Verified against the running API, not assumed.
  test('cl_pat_033 means the account has no hospital record', () async {
    final stay = await _loadWith(
      const ApiException(message: 'not linked', statusCode: 404, code: 'cl_pat_033'),
    );

    expect(stay, isA<MyStayNotLinked>());
  });

  test('cl_pat_034 means linked but not admitted', () async {
    final stay = await _loadWith(
      const ApiException(message: 'no current stay', statusCode: 404, code: 'cl_pat_034'),
    );

    expect(stay, isA<MyStayNoAdmission>());
  });

  test('a 404 with no code falls back to not admitted', () async {
    final stay = await _loadWith(const ApiException(message: 'gone', statusCode: 404));

    expect(stay, isA<MyStayNoAdmission>());
  });

  test('anything else surfaces as a failure the screen can retry', () async {
    final service = FakePatientService()
      ..admissionResult = const ApiException(message: 'server fell over', statusCode: 500);
    final controller = MyStayController(service);

    await controller.load();

    expect(controller.state, isA<AsyncFailed<MyStay>>());
  });

  test('an admission is passed straight through', () async {
    final stay = await _loadWith(const MyAdmission(
      admissionId: 'a1',
      status: AdmissionStatus.admitted,
      statusText: 'Admitted',
      detailsComplete: true,
      missingFields: [],
      wardName: 'ICU-1',
    ));

    expect((stay! as MyStayCurrent).admission.wardName, 'ICU-1');
  });
}
