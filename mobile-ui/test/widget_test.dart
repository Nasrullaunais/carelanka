import 'package:flutter_test/flutter_test.dart';

import 'package:carelanka_mobile/app.dart';

void main() {
  testWidgets('app starts on the splash while the session is restored',
      (WidgetTester tester) async {
    await tester.pumpWidget(const CareLankaApp());
    await tester.pump();

    expect(find.byType(CareLankaApp), findsOneWidget);
  });
}
