import http from 'k6/http';
import { fail } from 'k6';
import { BASE_URL, JSON_HEADERS, PATIENT_PASSWORD, STAFF_PASSWORD } from './config.js';

// Call these from setup(), never from the default function: login is rate-limited per IP,
// so logging in once per virtual user iteration measures the 429s, not the endpoint.
export function loginStaff(email, password = STAFF_PASSWORD) {
  return tokenFrom(http.post(`${BASE_URL}/auth/login`, JSON.stringify({ email, password }),
    { headers: JSON_HEADERS, tags: { name: 'auth/login' } }), email);
}

export function loginPatient(username, password = PATIENT_PASSWORD) {
  return tokenFrom(http.post(`${BASE_URL}/auth/patient/login`, JSON.stringify({ username, password }),
    { headers: JSON_HEADERS, tags: { name: 'auth/patient/login' } }), username);
}

export function authHeaders(token) {
  return { ...JSON_HEADERS, Authorization: `Bearer ${token}` };
}

function tokenFrom(res, who) {
  if (res.status !== 200) {
    fail(`login failed for ${who}: HTTP ${res.status} ${res.body}`);
  }
  return res.json('access_token');
}
