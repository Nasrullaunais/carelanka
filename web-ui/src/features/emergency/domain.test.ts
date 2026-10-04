import { describe, expect, it } from 'vitest';
import { shortAddress } from './domain';

describe('shortAddress', () => {
  it('keeps the place and road of a long geocoded address', () => {
    expect(shortAddress('UCSC, Philip Gunawardena Mawatha (Reid Avenue), Independence Square, Town Hall, Cinnamon Gardens, Colombo, Western Province, 00700, Sri Lanka'))
      .toBe('UCSC, Philip Gunawardena Mawatha (Reid Avenue)');
  });

  it('leaves a short address as it is', () => {
    expect(shortAddress('Galle Face')).toBe('Galle Face');
  });
});
