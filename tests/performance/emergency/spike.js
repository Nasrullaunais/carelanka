// EM-PERF-02: a sudden jump from 10 to 120 users, then back to 10 to see whether the API recovers.
import { loginAll, emergencyReadMix, endpointThresholds } from './common.js';
import { writeSummary } from './summary.js';

export const options = {
  scenarios: {
    spike: {
      executor: 'ramping-vus',
      startVUs: 10,
      stages: [
        { duration: '30s', target: 10 },
        { duration: '10s', target: 120 },
        { duration: '40s', target: 120 },
        { duration: '10s', target: 10 },
        { duration: '40s', target: 10 },
      ],
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

export const handleSummary = writeSummary('spike');
