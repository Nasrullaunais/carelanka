import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/core/widgets/async_data.dart';
import 'package:carelanka_mobile/features/patient/services/patient_service.dart';
import 'package:carelanka_mobile/features/patient/state/my_stay_controller.dart';
import 'package:carelanka_mobile/services/api_client/care_lanka_api.dart';
import 'package:carelanka_mobile/services/api_client/models/admission_status.dart';
import 'package:carelanka_mobile/services/api_client/models/my_admission.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

class _FakePatientService extends PatientService {
  _FakePatientService(this._result) : super(CareLankaApi(Dio()));

  final Object _result;

  @override
  Future<MyAdmission> loadMyAdmission() async {
    if (_result is MyAdmission) return _result;
    throw _result as ApiException;
  }
}

MyStay _stayFrom(AsyncData<MyStay> state) => (state as AsyncReady<MyStay>).value;

void main() {
  test('a 404 carrying cl_pat_033 means the account has no record, not an error', () async {
    final controller = MyStayController(_FakePatientService(
      const ApiException(message: 'not linked', statusCode: 404, code: 'cl_pat_033'),
    ));

    await controller.load();

    expect(_stayFrom(controller.state), isA<MyStayNotLinked>());
  });

  test('a plain 404 means linked but not admitted', () async {
    final controller = MyStayController(_FakePatientService(
      const ApiException(message: 'no admission', statusCode: 404),
    ));

    await controller.load();

    expect(_stayFrom(controller.state), isA<MyStayNoAdmission>());
  });

  test('anything else surfaces as a failure the screen can retry', () async {
    final controller = MyStayController(_FakePatientService(
      const ApiException(message: 'server fell over', statusCode: 500),
    ));

    await controller.load();

    expect(controller.state, isA<AsyncFailed<MyStay>>());
  });

  test('an admission is passed straight through', () async {
    const admission = MyAdmission(
      admissionId: 'a1',
      status: AdmissionStatus.admitted,
      statusText: 'Admitted',
      detailsComplete: true,
      missingFields: [],
      wardName: 'ICU-1',
    );
    final controller = MyStayController(_FakePatientService(admission));

    await controller.load();

    expect((_stayFrom(controller.state) as MyStayCurrent).admission.wardName, 'ICU-1');
  });
}
