import { sleep } from 'k6';
import exec from 'k6/execution';
import { loginStaff, resolveDemoPatientId, equipmentReadMix, SAFETY_THRESHOLDS } from './common.js';
import { summaryTo } from './summary.js';

// PERFORMANCE / LOAD testing: how response time holds up as concurrent users climb
// from 1 to 40, in steps - distinct from stress.js, which pushes past normal levels
// looking for a breaking point.
const LEVELS = [1, 5, 10, 20, 40];
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
  const token = loginStaff();
  return { token, patientId: resolveDemoPatientId(token), startedAt: Date.now() };
}

export default function (data) {
  const elapsed = (Date.now() - data.startedAt) / 1000;
  const index = Math.min(Math.floor(elapsed / STEP_SECONDS), stageNames.length - 1);
  exec.vu.metrics.tags.stage = stageNames[index];

  equipmentReadMix(data.token, data.patientId);
  sleep(1);
}
