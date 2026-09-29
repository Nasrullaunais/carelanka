import 'package:carelanka_mobile/features/patient/validation/patient_fields.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('validateNic', () {
    for (final nic in ['199534501V', '199745600321', '200331400589', 'N1234567']) {
      test('accepts $nic', () => expect(validateNic(nic), isNull));
    }

    for (final nic in ['2947382939772v', '199534501Z']) {
      test('refuses $nic rather than taking it for a passport', () {
        expect(validateNic(nic), contains('passport number starting with a letter'));
      });
    }

    test('refuses a twelve-digit NIC whose year cannot be a birth year', () {
      expect(validateNic('294738293977'), contains('2947 is not a possible one'));
    });
  });

  group('validateDateOfBirthAgainstNic', () {
    test('accepts an old NIC whose two digits are the birth year', () {
      expect(validateDateOfBirthAgainstNic(DateTime(1992, 3, 25), '927654321V'), isNull);
    });

    test('accepts a twelve-digit NIC for someone born before 2000', () {
      expect(validateDateOfBirthAgainstNic(DateTime(1997, 11, 2), '199745600321'), isNull);
    });

    test('refuses an old NIC for someone born in 2000 or later', () {
      expect(validateDateOfBirthAgainstNic(DateTime(2003, 5, 10), '997654321V'),
          contains('twelve-digit NIC'));
    });

    test('refuses a birth year the NIC does not give', () {
      expect(validateDateOfBirthAgainstNic(DateTime(2002, 1, 1), '200331400589'),
          'The NIC gives a birth year of 2003, but the date of birth is in 2002. Check both.');
    });
  });
}
