import 'package:flutter/foundation.dart';

enum SessionSignal { expired, passwordChangeRequired }

// Lets the network layer signal an expired session without importing the auth controller, and vice versa.
class SessionExpiry extends ChangeNotifier {
  SessionSignal? _last;

  SessionSignal? get last => _last;

  void expire() => _signal(SessionSignal.expired);

  void requirePasswordChange() => _signal(SessionSignal.passwordChangeRequired);

  void _signal(SessionSignal signal) {
    _last = signal;
    notifyListeners();
  }
}
