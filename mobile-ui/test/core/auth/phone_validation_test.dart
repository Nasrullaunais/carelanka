import 'package:carelanka_mobile/core/auth/auth_form.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('accepts both shapes a Sri Lankan mobile number is written in', () {
    expect(validatePhoneNumber('+94771234567'), isNull);
    expect(validatePhoneNumber('0771234567'), isNull);
    expect(validatePhoneNumber(' +94 77 123 4567 '), isNull);
  });

  test('rejects a number that is the wrong length or shape', () {
    expect(validatePhoneNumber('77123456'), isNotNull);
    expect(validatePhoneNumber('+9477123456789'), isNotNull);
    expect(validatePhoneNumber('not a number'), isNotNull);
  });

  test('an empty field asks for the number rather than complaining about format', () {
    expect(validatePhoneNumber(''), 'Enter your phone number');
    expect(validatePhoneNumber(null), 'Enter your phone number');
  });
}
