import { sleep } from 'k6';
import {
  loginStaff,
  resolveDemoPatientId,
  equipmentReadMix,
  readThresholds,
  SAFETY_THRESHOLDS,
} from './common.js';
import { summaryTo } from './summary.js';

// LOAD testing: one user, steady state, for a full minute - what normal, everyday
// traffic on the equipment register looks like, as a baseline to compare ramp.js
// and stress.js against.
export const options = {
  vus: 1,
  duration: '60s',
  thresholds: {
    ...SAFETY_THRESHOLDS,
    ...readThresholds(),
    http_req_duration: [{ threshold: 'p(95)<5000', abortOnFail: true, delayAbortEval: '10s' }],
  },
};

export const handleSummary = summaryTo('baseline');

export function setup() {
  const token = loginStaff();
  return { token, patientId: resolveDemoPatientId(token) };
}

export default function (data) {
  equipmentReadMix(data.token, data.patientId);
  sleep(1);
}
