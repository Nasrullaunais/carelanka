import http from 'k6/http';
import { check } from 'k6';

function requireEnv(name) {
  const value = __ENV[name];
  if (!value) {
    throw new Error(`${name} is not set. Set it in the shell session before running k6.`);
  }
  return value;
}

export const BASE_URL = requireEnv('BASE_URL').replace(/\/+$/, '');

const JSON_HEADERS = { 'Content-Type': 'application/json' };

export function authHeaders(token) {
  return { ...JSON_HEADERS, Authorization: `Bearer ${token}` };
}

function login(path, body) {
  const res = http.post(`${BASE_URL}/api/auth/${path}`, JSON.stringify(body), {
    headers: JSON_HEADERS,
    tags: { ep: 'login_setup' },
  });
  const ok = check(res, { 'login returned 200': (r) => r.status === 200 });
  if (!ok) {
    throw new Error(`Login on ${path} failed with status ${res.status}. Stopping before any load starts.`);
  }
  return res.json('access_token');
}

export function loginStaff() {
  return login('login', {
    email: requireEnv('QM_STAFF_USER'),
    password: requireEnv('QM_STAFF_PASSWORD'),
  });
}

export function loginPatient() {
  return login('patient/login', {
    username: requireEnv('QM_PATIENT_USER'),
    password: requireEnv('QM_PATIENT_PASSWORD'),
  });
}

export function get(token, path, ep) {
  const res = http.get(`${BASE_URL}${path}`, { headers: authHeaders(token), tags: { ep } });
  check(res, { [`${ep} is 200`]: (r) => r.status === 200 });
  return res;
}

export const STAFF_READS = [
  { ep: 'patients_list', path: '/api/patients?page=1&pageSize=20' },
  { ep: 'patients_search', path: '/api/patients?search=QM%20Test&page=1&pageSize=20' },
  { ep: 'worklist', path: '/api/patient-worklist?page=1&pageSize=20' },
  { ep: 'ward_capacity', path: '/api/capacity/wards' },
  { ep: 'wards_list', path: '/api/wards' },
  { ep: 'bed_availability', path: '/api/bed-availability?page=1&pageSize=50' },
  { ep: 'appointments_list', path: '/api/appointments?page=1&pageSize=20' },
  { ep: 'care_recommendations', path: '/api/care-recommendations?page=1&pageSize=20' },
  { ep: 'billing_outstanding', path: '/api/billing/outstanding?page=1&pageSize=20' },
];

export const READ_ENDPOINT_NAMES = STAFF_READS.map((r) => r.ep);

export function staffReadMix(token) {
  const pick = STAFF_READS[Math.floor(Math.random() * STAFF_READS.length)];
  return get(token, pick.path, pick.ep);
}

export function readThresholds() {
  const thresholds = {};
  for (const ep of READ_ENDPOINT_NAMES) {
    thresholds[`http_req_duration{ep:${ep}}`] = ['p(95)<500'];
  }
  return thresholds;
}

export const SAFETY_THRESHOLDS = {
  http_req_failed: [{ threshold: 'rate<0.05', abortOnFail: true, delayAbortEval: '20s' }],
};
