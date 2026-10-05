import { sleep } from 'k6';
import { summaryTo } from './summary.js';
import {
  loginStaff,
  resolveDemoPatientId,
  equipmentReadMix,
  readThresholds,
  SAFETY_THRESHOLDS,
} from './common.js';

export const options = {
  vus: 3,
  duration: '30s',
  thresholds: {
    ...SAFETY_THRESHOLDS,
    ...readThresholds(),
    http_req_duration: [{ threshold: 'p(95)<5000', abortOnFail: true, delayAbortEval: '10s' }],
  },
};

export const handleSummary = summaryTo('smoke');

export function setup() {
  const token = loginStaff();
  return { token, patientId: resolveDemoPatientId(token) };
}

export default function (data) {
  equipmentReadMix(data.token, data.patientId);
  sleep(1);
}
