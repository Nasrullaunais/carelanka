import 'package:flutter/foundation.dart';

// Lets the network layer signal an expired session without importing the auth controller, and vice versa.
class SessionExpiry extends ChangeNotifier {
  void expire() => notifyListeners();
}
