import http from 'k6/http';
import { check, sleep } from 'k6';
import { summaryTo } from './summary.js';
import { BASE_URL, authHeaders, loginPatient, SAFETY_THRESHOLDS } from './common.js';

const READS = [
  { ep: 'me_profile', path: '/api/me/profile' },
  { ep: 'me_admission', path: '/api/me/admission', accept404: true },
  { ep: 'me_history', path: '/api/me/history?page=1&pageSize=20' },
  { ep: 'me_appointments', path: '/api/me/appointments?page=1&pageSize=20' },
];

const allow404 = http.expectedStatuses(200, 404);

export const options = {
  vus: 5,
  duration: '60s',
  thresholds: {
    ...SAFETY_THRESHOLDS,
    'http_req_duration{ep:me_profile}': ['p(95)<500'],
    'http_req_duration{ep:me_admission}': ['p(95)<500'],
    'http_req_duration{ep:me_history}': ['p(95)<500'],
    'http_req_duration{ep:me_appointments}': ['p(95)<500'],
    http_req_duration: [{ threshold: 'p(95)<5000', abortOnFail: true, delayAbortEval: '10s' }],
  },
};

export const handleSummary = summaryTo('patient-reads');

export function setup() {
  return { token: loginPatient() };
}

export default function (data) {
  for (const read of READS) {
    const params = { headers: authHeaders(data.token), tags: { ep: read.ep } };
    if (read.accept404) {
      params.responseCallback = allow404;
    }
    const res = http.get(`${BASE_URL}${read.path}`, params);
    const expected = read.accept404 ? [200, 404] : [200];
    check(res, { [`${read.ep} status ok`]: (r) => expected.includes(r.status) });
  }
  sleep(1);
}
