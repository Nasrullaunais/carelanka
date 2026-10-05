import http from 'k6/http';
import { check, sleep } from 'k6';

function requireEnv(name) {
  const value = __ENV[name];
  if (!value) {
    throw new Error(`${name} is not set. Export it in the shell before running k6.`);
  }
  return value;
}

export const BASE_URL = requireEnv('BASE_URL').replace(/\/+$/, '');

const JSON_HEADERS = { 'Content-Type': 'application/json' };

function login(path, body) {
  const res = http.post(`${BASE_URL}/api/auth/${path}`, JSON.stringify(body), {
    headers: JSON_HEADERS,
    tags: { ep: 'login_setup' },
  });
  if (res.status !== 200) {
    throw new Error(`Login on ${path} failed with status ${res.status}. Stopping before any load starts.`);
  }
  return res.json('access_token');
}

// Logs in once per role before the load starts, so the run measures reads, not logins.
export function loginAll() {
  return {
    manager: login('login', { email: requireEnv('QM_DM_USER'), password: requireEnv('QM_DM_PASSWORD') }),
    crew: login('login', { email: requireEnv('QM_CREW_USER'), password: requireEnv('QM_CREW_PASSWORD') }),
    patient: login('patient/login', {
      username: requireEnv('QM_PATIENT_USER'),
      password: requireEnv('QM_PATIENT_PASSWORD'),
    }),
  };
}

const REPORT_RANGE = 'from=2026-09-28&to=2026-10-05';

export const READS = {
  manager: [
    { ep: 'call_board', path: '/api/emergency-calls?page=1&pageSize=20' },
    { ep: 'call_board_waiting', path: '/api/emergency-calls?status=received&unassignedOnly=true&page=1&pageSize=20' },
    { ep: 'call_board_search', path: '/api/emergency-calls?search=Colombo&page=1&pageSize=20' },
    { ep: 'ambulances_nearest', path: '/api/ambulances?nearToLatitude=6.9271&nearToLongitude=79.8612&sortBy=distance&page=1&pageSize=20' },
    { ep: 'fleet_map', path: '/api/fleet-map' },
    { ep: 'dispatch_proposals', path: '/api/dispatch-proposals?page=1&pageSize=20' },
    { ep: 'cancellation_requests', path: '/api/emergency-cancellation-requests?page=1&pageSize=20' },
    { ep: 'report_response_times', path: `/api/reports/emergency/response-times?${REPORT_RANGE}` },
    { ep: 'report_fleet_utilisation', path: `/api/reports/emergency/fleet-utilisation?${REPORT_RANGE}` },
  ],
  crew: [
    { ep: 'crew_my_ambulance', path: '/api/ambulances/mine' },
    { ep: 'crew_active_run', path: '/api/me/dispatches/active' },
    { ep: 'crew_run_history', path: '/api/me/dispatches/history?page=1&pageSize=20' },
  ],
  patient: [
    { ep: 'patient_my_calls', path: '/api/me/emergency-calls?page=1&pageSize=20' },
  ],
};

export const ENDPOINT_NAMES = Object.values(READS).flat().map((read) => read.ep);

function pick(list) {
  return list[Math.floor(Math.random() * list.length)];
}

// Duty managers drive most of the traffic; crews and patients poll their own screens.
export function emergencyReadMix(tokens) {
  const roll = Math.random();
  const role = roll < 0.7 ? 'manager' : roll < 0.9 ? 'crew' : 'patient';
  const read = pick(READS[role]);
  const res = http.get(`${BASE_URL}${read.path}`, {
    headers: { Authorization: `Bearer ${tokens[role]}` },
    tags: { ep: read.ep, role },
  });
  check(res, { [`${read.ep} is 200`]: (r) => r.status === 200 });
  sleep(1);
  return res;
}

export function endpointThresholds() {
  const thresholds = {
    // The 1% target, plus a stop if failures pass 5%, so a run cannot pile onto a struggling shared server.
    http_req_failed: ['rate<0.01', { threshold: 'rate<0.05', abortOnFail: true, delayAbortEval: '20s' }],
    'http_req_duration{ep:login_setup}': ['max<5000'],
  };
  for (const ep of ENDPOINT_NAMES) {
    thresholds[`http_req_duration{ep:${ep}}`] = ['p(95)<500'];
  }
  return thresholds;
}
