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

export function loginStaff() {
  const res = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({
      email: requireEnv('EQ_STAFF_USER'),
      password: requireEnv('EQ_STAFF_PASSWORD'),
    }),
    { headers: JSON_HEADERS, tags: { ep: 'login_setup' } },
  );
  const ok = check(res, { 'login returned 200': (r) => r.status === 200 });
  if (!ok) {
    throw new Error(`Login failed with status ${res.status}. Stopping before any load starts.`);
  }
  return res.json('access_token');
}

// Lab reports can only be listed for one patient at a time (GET /api/lab-reports?patientId=...),
// so there is no patient-free way to read them. Rather than hard-code a demo patient id that
// might not exist in every environment, grab whichever patient sorts first - any seeded database
// has at least one. If there truly are none, the lab-reports endpoint is left out of the mix
// below instead of failing the whole run.
export function resolveDemoPatientId(token) {
  const res = http.get(`${BASE_URL}/api/patients?page=1&pageSize=1`, {
    headers: authHeaders(token),
    tags: { ep: 'patients_search_setup' },
  });
  const ok = check(res, { 'patient lookup returned 200': (r) => r.status === 200 });
  if (!ok) {
    return null;
  }
  const items = res.json('items');
  return items && items.length > 0 ? items[0].id : null;
}

export function get(token, path, ep) {
  const res = http.get(`${BASE_URL}${path}`, { headers: authHeaders(token), tags: { ep } });
  check(res, { [`${ep} is 200`]: (r) => r.status === 200 });
  return res;
}

// Six read endpoints the equipment manager account can reach: equipment-items and
// equipment-categories are the register itself; pharmacy-items and warnings are the two other
// lists this role works from day to day; lab-reports and the patient lookup behind it cover the
// laboratory side of the component. Endpoints that need a required status enum (prescriptions)
// were left out, to keep every request in the mix self-contained.
export const EQUIPMENT_READS = [
  { ep: 'equipment_items_list', path: '/api/equipment-items?page=1&pageSize=20' },
  { ep: 'equipment_items_search', path: '/api/equipment-items?search=Vent&page=1&pageSize=20' },
  { ep: 'equipment_categories_list', path: '/api/equipment-categories' },
  { ep: 'pharmacy_items_list', path: '/api/pharmacy-items?page=1&pageSize=20' },
  { ep: 'warnings_list', path: '/api/warnings?page=1&pageSize=20' },
  {
    ep: 'lab_reports_list',
    path: '/api/lab-reports?patientId={patientId}&page=1&pageSize=20',
    needsPatientId: true,
  },
];

export const READ_ENDPOINT_NAMES = EQUIPMENT_READS.map((r) => r.ep);

export function equipmentReadMix(token, patientId) {
  const pool = patientId ? EQUIPMENT_READS : EQUIPMENT_READS.filter((r) => !r.needsPatientId);
  const pick = pool[Math.floor(Math.random() * pool.length)];
  const path = pick.needsPatientId ? pick.path.replace('{patientId}', patientId) : pick.path;
  return get(token, path, pick.ep);
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
