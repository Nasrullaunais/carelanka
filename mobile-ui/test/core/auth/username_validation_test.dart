import 'package:carelanka_mobile/core/auth/auth_form.dart';
import 'package:flutter_test/flutter_test.dart';

/// Mirrors `UsernameRules` on the server. If these two ever disagree the app
/// either rejects a username the API would have taken, or sends one it refuses.
void main() {
  test('accepts the characters the server allows', () {
    expect(validateUsername('chathura.w'), isNull);
    expect(validateUsername('nimal_silva-2'), isNull);
    expect(validateUsername(' kaveesha '), isNull);
  });

  test('rejects anything outside that set', () {
    expect(validateUsername('has spaces'), isNotNull);
    expect(validateUsername('user@example.com'), isNotNull);
    expect(validateUsername('+94771234567'), isNotNull);
  });

  test('holds the server to its own length limits', () {
    expect(validateUsername('ab'), 'Use at least 3 characters');
    expect(validateUsername('a' * 50), isNull);
    expect(validateUsername('a' * 51), 'Use 50 characters or fewer');
  });

  test('an empty field asks for a username rather than complaining about format', () {
    expect(validateUsername(''), 'Choose a username');
    expect(validateUsername(null), 'Choose a username');
  });
}
