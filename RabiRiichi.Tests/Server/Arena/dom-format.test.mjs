import assert from 'node:assert/strict';
import test from 'node:test';
import { formatInt, formatSigned } from '../../../RabiRiichi.Arena/wwwroot/dom.js';
import { getBcp47Locale } from '../../../RabiRiichi.Arena/wwwroot/i18n/index.js';

test('decimal score strings and bigint values format without losing low digits', () => {
  const amount = (1n << 256n) + 123n;
  const expected = new Intl.NumberFormat(getBcp47Locale()).format(amount);
  assert.equal(formatInt(amount.toString()), expected);
  assert.equal(formatInt(amount), expected);
  assert.equal(formatSigned(amount.toString()), `+${expected}`);
  assert.equal(formatSigned((-amount).toString()), `-${expected}`);
  assert.equal(formatSigned(amount.toString(), 2), `+${expected}.00`);
});

test('ordinary numeric values and absent values retain their formatting', () => {
  assert.equal(formatInt(null), '0');
  assert.equal(formatInt(1.6), '2');
  assert.equal(formatSigned(-1.25, 2), '-1.25');
  assert.equal(formatSigned('0'), '0');
});
