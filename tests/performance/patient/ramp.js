import { sleep } from 'k6';
import exec from 'k6/execution';
import { loginStaff, SAFETY_THRESHOLDS } from './common.js';
import { staffIteration } from './mix.js';
import { summaryTo } from './summary.js';

const CREATE_AT = [150, 600, 1200, 2000, 3000, 4000, 5000, 6000];
const LEVELS = [1, 5, 10, 20, 40, 60];
const STEP_SECONDS = 60;

const stageNames = LEVELS.map((vus) => `vu${String(vus).padStart(2, '0')}`);

const stageThresholds = {};
for (const name of stageNames) {
  stageThresholds[`http_req_duration{stage:${name}}`] = ['p(95)<500'];
  stageThresholds[`http_req_failed{stage:${name}}`] = ['rate<0.01'];
}

export const options = {
  scenarios: {
    ramp: {
      executor: 'ramping-vus',
      startVUs: 1,
      gracefulRampDown: '10s',
      stages: [
        ...LEVELS.flatMap((vus) => [
          { duration: '15s', target: vus },
          { duration: '45s', target: vus },
        ]),
        { duration: '15s', target: 0 },
      ],
    },
  },
  thresholds: {
    ...SAFETY_THRESHOLDS,
    ...stageThresholds,
    http_req_duration: [{ threshold: 'p(95)<5000', abortOnFail: true, delayAbortEval: '20s' }],
  },
};

export const handleSummary = summaryTo('ramp');

export function setup() {
  return { token: loginStaff(), startedAt: Date.now() };
}

export default function (data) {
  const elapsed = (Date.now() - data.startedAt) / 1000;
  const index = Math.min(Math.floor(elapsed / STEP_SECONDS), stageNames.length - 1);
  exec.vu.metrics.tags.stage = stageNames[index];

  staffIteration(data.token, 'ramp', CREATE_AT);
  sleep(1);
}
