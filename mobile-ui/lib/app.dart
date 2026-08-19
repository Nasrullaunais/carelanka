import 'package:flutter/material.dart';

/// Root widget of the CareLanka mobile app.
///
/// This is a placeholder shell so the project runs. Once the router is set up
/// in `core/routing/`, swap `MaterialApp` for `MaterialApp.router` and pass the
/// shared router config. See mobile-ui/README.md for who owns what.
class CareLankaApp extends StatelessWidget {
  const CareLankaApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'CareLanka',
      theme: ThemeData(useMaterial3: true, colorSchemeSeed: Colors.teal),
      home: const Scaffold(
        body: Center(child: Text('CareLanka — structure ready, no screens yet')),
      ),
    );
  }
}
