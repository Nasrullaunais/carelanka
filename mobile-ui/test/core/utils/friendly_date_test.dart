import 'package:carelanka_mobile/core/utils/friendly_date.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  final now = DateTime(2026, 9, 14, 10, 0);

  group('relativeDay', () {
    test('names today, tomorrow and yesterday instead of dating them', () {
      expect(FriendlyDate.relativeDay(DateTime(2026, 9, 14, 18), now: now), 'Today');
      expect(FriendlyDate.relativeDay(DateTime(2026, 9, 15, 9), now: now), 'Tomorrow');
      expect(FriendlyDate.relativeDay(DateTime(2026, 9, 13, 9), now: now), 'Yesterday');
    });

    test('anything further out gets a date', () {
      expect(FriendlyDate.relativeDay(DateTime(2026, 9, 18, 9), now: now), 'Fri, 18 Sep');
    });

    test('11pm tonight is today and 1am tomorrow is tomorrow', () {
      // The reason this counts calendar days rather than 24-hour blocks: both
      // of these are under a day away, and they are not the same day.
      expect(FriendlyDate.relativeDay(DateTime(2026, 9, 14, 23), now: now), 'Today');
      expect(FriendlyDate.relativeDay(DateTime(2026, 9, 15, 1), now: now), 'Tomorrow');
    });
  });

  group('countdown', () {
    test('counts forward in the largest unit that fits', () {
      expect(FriendlyDate.countdown(DateTime(2026, 9, 18, 10), now: now), 'In 4 days');
      expect(FriendlyDate.countdown(DateTime(2026, 9, 14, 13), now: now), 'In 3 hours');
      expect(FriendlyDate.countdown(DateTime(2026, 9, 14, 10, 40), now: now), 'In 40 minutes');
    });

    test('singular and plural both read properly', () {
      expect(FriendlyDate.countdown(DateTime(2026, 9, 15, 10), now: now), 'In 1 day');
      expect(FriendlyDate.countdown(DateTime(2026, 9, 14, 11), now: now), 'In 1 hour');
    });

    test('the past counts backwards', () {
      expect(FriendlyDate.countdown(DateTime(2026, 9, 11, 10), now: now), '3 days ago');
      expect(FriendlyDate.countdown(DateTime(2026, 9, 14, 8), now: now), '2 hours ago');
    });

    test('under a minute either way is simply now', () {
      expect(FriendlyDate.countdown(DateTime(2026, 9, 14, 10, 0, 30), now: now), 'Now');
      expect(FriendlyDate.countdown(DateTime(2026, 9, 14, 9, 59, 40), now: now), 'Now');
    });
  });

  test('relativeDayAndTime puts the two together', () {
    expect(
      FriendlyDate.relativeDayAndTime(DateTime(2026, 9, 15, 9), now: now),
      'Tomorrow at 9:00 AM',
    );
  });

  test('full spells out the whole thing for a record', () {
    expect(FriendlyDate.full(DateTime(2026, 9, 18, 9)), 'Fri, 18 Sep 2026 at 9:00 AM');
  });

  test('date keeps the day/month/year order the forms already use', () {
    expect(FriendlyDate.date(DateTime(2026, 9, 8)), '08/09/2026');
  });
}
