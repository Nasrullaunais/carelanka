import { describe, expect, it } from 'vitest';
import { nicBirthYearProblem, nicProblem } from './identifiers';

describe('nicProblem', () => {
  it.each(['199534501V', '199534501x', '199745600321', '200331400589', 'N1234567'])(
    'accepts %s',
    (nic) => {
      expect(nicProblem(nic)).toBeNull();
    },
  );

  it.each(['2947382939772v', '199534501Z', '12345'])('refuses %s as a mistyped NIC', (nic) => {
    expect(nicProblem(nic)).toMatch(/passport number starting with a letter/);
  });

  it('refuses a twelve-digit NIC whose year cannot be a birth year', () => {
    expect(nicProblem('294738293977')).toMatch(/2947 is not a possible one/);
  });
});

describe('nicBirthYearProblem', () => {
  it('accepts an old NIC whose two digits are the birth year', () => {
    expect(nicBirthYearProblem('927654321V', 1992)).toBeNull();
  });

  it('accepts a twelve-digit NIC for someone born before 2000', () => {
    expect(nicBirthYearProblem('199745600321', 1997)).toBeNull();
  });

  it('refuses an old NIC for someone born in 2000 or later', () => {
    expect(nicBirthYearProblem('997654321V', 2003)).toMatch(/twelve-digit NIC/);
  });

  it('refuses a birth year the NIC does not give', () => {
    expect(nicBirthYearProblem('200331400589', 2002)).toBe(
      'The NIC gives a birth year of 2003, but the date of birth is in 2002. Check both.',
    );
  });

  it('does not check a passport against the birth year', () => {
    expect(nicBirthYearProblem('N1234567', 2003)).toBeNull();
  });
});
