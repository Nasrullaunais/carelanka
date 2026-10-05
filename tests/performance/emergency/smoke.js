// EM-PERF-00: 3 users for 30 seconds, to prove the scripts and logins work before the bigger runs.
import { loginAll, emergencyReadMix, endpointThresholds } from './common.js';
import { writeSummary } from './summary.js';

export const options = {
  vus: 3,
  duration: '30s',
  thresholds: endpointThresholds(),
};

export function setup() {
  return loginAll();
}

export default function (tokens) {
  emergencyReadMix(tokens);
}

export const handleSummary = writeSummary('smoke');
