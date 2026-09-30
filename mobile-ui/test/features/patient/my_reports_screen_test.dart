import 'dart:typed_data';

import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/core/network/file_open_exception.dart';
import 'package:carelanka_mobile/core/theme/app_theme.dart';
import 'package:carelanka_mobile/features/patient/screens/my_reports_screen.dart';
import 'package:carelanka_mobile/features/patient/state/lab_reports_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/my_lab_report.dart';
import 'package:carelanka_mobile/services/api_client/models/my_lab_report_paged_result.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'fake_patient_service.dart';

final _report = MyLabReport(
  id: 'report-1',
  testName: 'Lipid Profile',
  fileName: 'lipid-profile.pdf',
  contentType: 'application/pdf',
  byteSize: 1024,
  createdAt: DateTime.utc(2026, 9, 1),
);

MyLabReportPagedResult _pageOf(MyLabReport report) => MyLabReportPagedResult(
      items: [report],
      page: 1,
      pageSize: 20,
      totalItems: 1,
      totalPages: 1,
    );

// This is the widget-test counterpart to Issue #140: it passes fakes for
// both the download step and the opener, so it never touches a real Dio
// client or the real open_filex plugin, per the issue's own test plan.
Widget app({
  required LabReportsController controller,
  required DownloadReportBytes downloadBytes,
  required OpenReportBytes openFileBytes,
}) {
  return MaterialApp(
    theme: AppTheme.light,
    home: MyReportsScreen(
      controller: controller,
      downloadBytes: downloadBytes,
      openFileBytes: openFileBytes,
    ),
  );
}

void main() {
  testWidgets('tapping a report downloads it and hands the bytes to the opener', (
    tester,
  ) async {
    final service = FakePatientService()..labReportsResult = _pageOf(_report);
    final bytes = Uint8List.fromList([1, 2, 3]);
    String? downloadedPath;
    Uint8List? openedBytes;
    String? openedContentType;
    String? openedFileName;

    await tester.pumpWidget(
      app(
        controller: LabReportsController(service)..load(),
        downloadBytes: (path) async {
          downloadedPath = path;
          return bytes;
        },
        openFileBytes: ({required bytes, required contentType, required fileName}) async {
          openedBytes = bytes;
          openedContentType = contentType;
          openedFileName = fileName;
        },
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Lipid Profile'));
    await tester.pumpAndSettle();

    expect(downloadedPath, '/me/lab-reports/report-1/file');
    expect(openedBytes, bytes);
    expect(openedContentType, 'application/pdf');
    expect(openedFileName, 'lipid-profile.pdf');
  });

  testWidgets('a FileOpenException from the opener shows its message in a snackbar', (
    tester,
  ) async {
    final service = FakePatientService()..labReportsResult = _pageOf(_report);

    await tester.pumpWidget(
      app(
        controller: LabReportsController(service)..load(),
        downloadBytes: (path) async => Uint8List(0),
        openFileBytes: ({required bytes, required contentType, required fileName}) async {
          throw const FileOpenException(
            'No app on this phone can open this file. Install a PDF viewer and try again.',
          );
        },
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Lipid Profile'));
    await tester.pumpAndSettle();

    expect(
      find.text('No app on this phone can open this file. Install a PDF viewer and try again.'),
      findsOneWidget,
    );
  });

  testWidgets('an ApiException from downloading still shows its message in a snackbar', (
    tester,
  ) async {
    final service = FakePatientService()..labReportsResult = _pageOf(_report);

    await tester.pumpWidget(
      app(
        controller: LabReportsController(service)..load(),
        downloadBytes: (path) async => throw const ApiException(
          message: 'Could not reach the server. Check your connection and try again.',
        ),
        openFileBytes: ({required bytes, required contentType, required fileName}) async {},
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Lipid Profile'));
    await tester.pumpAndSettle();

    expect(
      find.text('Could not reach the server. Check your connection and try again.'),
      findsOneWidget,
    );
  });
}
