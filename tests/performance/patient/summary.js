import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';

// Paths are relative to the folder k6 is started from; run-k6.ps1 starts it in this folder.
export function summaryTo(scenario) {
  return (data) => {
    const stamp = __ENV.QM_RUN_STAMP || new Date().toISOString().replace(/[:.]/g, '-');
    const base = `results/${scenario}-${stamp}`;
    const text = textSummary(data, { indent: ' ', enableColors: false });

    return {
      stdout: text,
      [`${base}.txt`]: text,
      [`${base}.json`]: JSON.stringify(data, null, 2),
    };
  };
}
