import 'package:flutter/foundation.dart';

/// Signals that the refresh token is gone or rejected and the user must log in
/// again.
///
/// This exists so the network layer can report an expired session without
/// importing the auth controller, and the auth controller can react without
/// importing the network layer.
class SessionExpiry extends ChangeNotifier {
  void expire() => notifyListeners();
}
