// Dates on the wire are UTC; people read local time. These four are the only places that
// difference is handled, so nowhere else has to think about it.

/**
 * The yyyy-mm-dd a `?date=` filter wants, for the UTC day a moment falls in.
 *
 * `toISOString` is UTC, which is deliberate and NOT the same as the local date: the column
 * stores UTC and the endpoint filters whole UTC days. Colombo is 5½ hours ahead, so at 3am
 * local this is still yesterday's date — which is honest, because that is the day the server
 * would count it in.
 */
export function utcDay(when: Date): string {
  return when.toISOString().slice(0, 10);
}

/** The time in the reader's own timezone. Nobody at a desk reads UTC. */
export function localTime(iso: string): string {
  return new Date(iso).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
}

/** Date and time together, for a list that is not filtered to one day. */
export function localDateTime(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/** The value a `datetime-local` input wants: local wall-clock, no timezone, no seconds. */
export function localInputValue(when: Date): string {
  const offset = when.getTimezoneOffset() * 60_000;
  return new Date(when.getTime() - offset).toISOString().slice(0, 16);
}
