import { sleep } from 'k6';
import exec from 'k6/execution';
import { Counter } from 'k6/metrics';
import {
  loginStaff,
  resolveDemoPatientId,
  equipmentReadMix,
  readThresholds,
  SAFETY_THRESHOLDS,
} from './common.js';
import { summaryTo } from './summary.js';

// STRESS testing: push well past baseline/ramp levels - 60 to 250 concurrent users -
// looking for the point where the API starts failing or rate-limiting, not just
// slowing down.
const LEVELS = [60, 100, 150, 200, 250];
const STEP_SECONDS = 60;

const stageNames = LEVELS.map((vus) => `vu${vus}`);

const rateLimited = new Counter('http_429');

const stageThresholds = {};
for (const name of stageNames) {
  stageThresholds[`http_req_duration{stage:${name}}`] = ['p(95)<500'];
  stageThresholds[`http_req_failed{stage:${name}}`] = ['rate<0.01'];
  stageThresholds[`http_429{stage:${name}}`] = ['count<1'];
}

export const options = {
  scenarios: {
    stress: {
      executor: 'ramping-vus',
      startVUs: LEVELS[0],
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
    ...readThresholds(),
    ...stageThresholds,
    http_429: ['count<1'],
    http_req_duration: [{ threshold: 'p(95)<5000', abortOnFail: true, delayAbortEval: '20s' }],
  },
};

export const handleSummary = summaryTo('stress');

export function setup() {
  const token = loginStaff();
  return { token, patientId: resolveDemoPatientId(token), startedAt: Date.now() };
}

export default function (data) {
  const elapsed = (Date.now() - data.startedAt) / 1000;
  const index = Math.min(Math.floor(elapsed / STEP_SECONDS), stageNames.length - 1);
  exec.vu.metrics.tags.stage = stageNames[index];

  const res = equipmentReadMix(data.token, data.patientId);
  if (res.status === 429) {
    rateLimited.add(1);
  }
  sleep(1);
}
