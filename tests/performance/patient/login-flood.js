import http from 'k6/http';
import { check } from 'k6';
import { summaryTo } from './summary.js';
import { BASE_URL } from './common.js';

const REFUSED_OR_LIMITED = http.expectedStatuses(401, 429);

export const options = {
  scenarios: {
    flood: {
      executor: 'per-vu-iterations',
      vus: 1,
      iterations: 25,
      maxDuration: '50s',
    },
  },
};

export const handleSummary = summaryTo('login-flood');

export default function () {
  const res = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({ email: 'qm-pt-flood@example.invalid', password: 'not-a-real-password' }),
    {
      headers: { 'Content-Type': 'application/json' },
      tags: { ep: 'login_flood' },
      responseCallback: REFUSED_OR_LIMITED,
    },
  );
  check(res, {
    'refused as 401 or limited as 429': (r) => r.status === 401 || r.status === 429,
    'limited (429)': (r) => r.status === 429,
  });
  console.log(`attempt ${__ITER + 1}: ${res.status}`);
}
