export const BASE_URL = (__ENV.BASE_URL || 'http://localhost:5231/api').replace(/\/$/, '');

export const STAFF_PASSWORD = __ENV.STAFF_PASSWORD || 'CareLanka#2026';
export const PATIENT_PASSWORD = __ENV.PATIENT_PASSWORD || 'Patient#2026';

export const JSON_HEADERS = { 'Content-Type': 'application/json' };
