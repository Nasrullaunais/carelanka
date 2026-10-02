import { sleep } from 'k6';
import { loginStaff, readThresholds, SAFETY_THRESHOLDS } from './common.js';
import { staffIteration } from './mix.js';
import { summaryTo } from './summary.js';

const CREATE_AT = [10, 40];

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
  return { token: loginStaff() };
}

export default function (data) {
  staffIteration(data.token, 'baseline', CREATE_AT);
  sleep(1);
}
