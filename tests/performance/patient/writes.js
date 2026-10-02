import http from 'k6/http';
import { check } from 'k6';
import { BASE_URL, authHeaders } from './common.js';

const expect400 = http.expectedStatuses(400);

function twelveDigitNic() {
  const year = 1960 + Math.floor(Math.random() * 40);
  let rest = '';
  for (let i = 0; i < 8; i++) {
    rest += Math.floor(Math.random() * 10);
  }
  return `${year}${rest}`;
}

export function createPatientThenBook(token, tag) {
  const create = http.post(
    `${BASE_URL}/api/patients`,
    JSON.stringify({
      full_name: `QM Test ${tag}`,
      nic: twelveDigitNic(),
      gender: 'other',
    }),
    { headers: authHeaders(token), tags: { ep: 'patient_create' } },
  );
  const created = check(create, { 'patient_create is 201': (r) => r.status === 201 });
  if (!created) {
    return;
  }

  const scheduledAt = new Date(Date.now() + 2 * 24 * 60 * 60 * 1000).toISOString();
  const book = http.post(
    `${BASE_URL}/api/appointments`,
    JSON.stringify({
      patient_id: create.json('id'),
      scheduled_at: scheduledAt,
      reason: 'QM Test load run',
    }),
    { headers: authHeaders(token), tags: { ep: 'appointment_create' } },
  );
  check(book, { 'appointment_create is 201': (r) => r.status === 201 });
}

export function sendInvalidWrite(token, variant) {
  const bodies = [
    { full_name: 'QM Test invalid no gender', nic: twelveDigitNic() },
    { full_name: 'QM Test invalid nic', nic: '12', gender: 'other' },
    { full_name: '', nic: twelveDigitNic(), gender: 'other' },
  ];
  const res = http.post(
    `${BASE_URL}/api/patients`,
    JSON.stringify(bodies[variant % bodies.length]),
    { headers: authHeaders(token), tags: { ep: 'patient_create_invalid' }, responseCallback: expect400 },
  );
  check(res, { 'invalid write is 400': (r) => r.status === 400 });
}
