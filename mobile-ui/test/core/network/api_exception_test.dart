import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

DioException _withBody(Object? body, {int? statusCode}) {
  final options = RequestOptions(path: '/me/admission');
  return DioException(
    requestOptions: options,
    response: statusCode == null
        ? null
        : Response<Object?>(requestOptions: options, statusCode: statusCode, data: body),
  );
}

void main() {
  test('reads the message and the machine-readable code out of problem+json', () {
    final error = ApiException.from(_withBody(
      {
        'title': 'Not Found',
        'status': 404,
        'detail': 'Your login is not linked to a hospital record yet.',
        'code': 'cl_pat_033',
      },
      statusCode: 404,
    ));

    expect(error.statusCode, 404);
    expect(error.code, 'cl_pat_033');
    expect(error.message, 'Your login is not linked to a hospital record yet.');
    expect(error.isNotFound, isTrue);
  });

  test('falls back to the title when there is no detail', () {
    final error = ApiException.from(_withBody({'title': 'Conflict'}, statusCode: 409));

    expect(error.message, 'Conflict');
    expect(error.isConflict, isTrue);
  });

  test('collects field errors from a validation problem', () {
    final error = ApiException.from(_withBody(
      {
        'title': 'One or more validation errors occurred.',
        'errors': {
          'nic': ['NIC is required.'],
        },
      },
      statusCode: 400,
    ));

    expect(error.fieldErrors['nic'], ['NIC is required.']);
  });

  test('a request that never reached the server is flagged, not silently dropped', () {
    final error = ApiException.from(_withBody(null));

    expect(error.isNetworkFailure, isTrue);
    expect(error.statusCode, isNull);
  });
}
