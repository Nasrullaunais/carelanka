import http from 'k6/http';
import { check } from 'k6';
import { BASE_URL } from './lib/config.js';
import { authHeaders, loginStaff } from './lib/auth.js';

// Not a load test. One request of each kind, to prove k6, the API, the database and the
// login helper all work before anyone writes a real scenario.
export const options = { vus: 1, iterations: 1 };

export function setup() {
  return { token: loginStaff('nurse.perera@carelanka.lk') };
}

export default function ({ token }) {
  const health = http.get(`${BASE_URL}/health`);
  check(health, {
    'health is 200': (r) => r.status === 200,
    'database is up': (r) => r.json('database') === 'up',
  });

  const me = http.get(`${BASE_URL}/auth/me`, { headers: authHeaders(token) });
  check(me, { 'token is accepted': (r) => r.status === 200 });
}
