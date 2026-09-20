import 'package:flutter/material.dart';

import 'app.dart';
import 'core/push/push_gateway.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(CareLankaApp(push: await FirebasePushGateway.create()));
}
