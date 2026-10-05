import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';

// One handleSummary per scenario, writing both a machine-readable JSON and a plain-text report
// into results/, named after the scenario and the run's timestamp (set once by run-k6.ps1 so
// every file from the same invocation shares a stamp).
export function summaryTo(scenario) {
  return function handleSummary(data) {
    const stamp = __ENV.EQ_RUN_STAMP || new Date().toISOString().replace(/[:.]/g, '-');
    const base = `results/${scenario}-${stamp}`;

    return {
      stdout: textSummary(data, { indent: ' ', enableColors: true }),
      [`${base}.json`]: JSON.stringify(data, null, 2),
      [`${base}.txt`]: textSummary(data, { indent: ' ', enableColors: false }),
    };
  };
}
