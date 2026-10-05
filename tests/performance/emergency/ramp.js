// EM-PERF-01: users added in six steps (1, 5, 10, 20, 40, 60), one minute at each step.
import { loginAll, emergencyReadMix, endpointThresholds } from './common.js';
import { writeSummary } from './summary.js';

const steps = [1, 5, 10, 20, 40, 60];

export const options = {
  scenarios: {
    ramp: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: steps.flatMap((users) => [
        { duration: '5s', target: users },
        { duration: '55s', target: users },
      ]),
      gracefulRampDown: '10s',
    },
  },
  thresholds: endpointThresholds(),
};

export function setup() {
  return loginAll();
}

export default function (tokens) {
  emergencyReadMix(tokens);
}

export const handleSummary = writeSummary('ramp');
