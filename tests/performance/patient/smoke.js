import { sleep } from 'k6';
import { summaryTo } from './summary.js';
import { loginStaff, staffReadMix, readThresholds, SAFETY_THRESHOLDS } from './common.js';

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
  return { token: loginStaff() };
}

export default function (data) {
  staffReadMix(data.token);
  sleep(1);
}
