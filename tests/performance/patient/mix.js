import exec from 'k6/execution';
import { staffReadMix } from './common.js';
import { createPatientThenBook, sendInvalidWrite } from './writes.js';

// createAt holds the exact iteration numbers that create a patient, so a full run can
// never create more patients than the list has entries.
export function staffIteration(token, scenario, createAt) {
  const n = exec.scenario.iterationInTest;

  if (createAt.includes(n)) {
    createPatientThenBook(token, `qm-pt-${scenario}-${n}`);
  } else if (n % 50 === 25) {
    sendInvalidWrite(token, n);
  } else {
    staffReadMix(token);
  }
}
