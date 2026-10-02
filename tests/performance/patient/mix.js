import exec from 'k6/execution';
import { staffReadMix } from './common.js';
import { createPatientThenBook, sendInvalidWrite } from './writes.js';

export function staffIteration(token) {
  const n = exec.scenario.iterationInTest;

  if (n % 100 === 0) {
    createPatientThenBook(token, `qm-pt-${n}`);
  } else if (n % 50 === 25) {
    sendInvalidWrite(token, n);
  } else {
    staffReadMix(token);
  }
}
