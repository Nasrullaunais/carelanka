import { textSummary } from 'https://jslib.k6.io/k6-summary/0.1.0/index.js';

export function writeSummary(name) {
  return (data) => {
    const stamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
    const base = `results/${name}-${stamp}`;
    // setup_data holds the login tokens, so it never goes into a saved file.
    const { setup_data: _tokens, ...saved } = data;
    return {
      stdout: textSummary(data, { indent: ' ', enableColors: false }),
      [`${base}.txt`]: textSummary(data, { indent: ' ', enableColors: false }),
      [`${base}.json`]: JSON.stringify(saved, null, 2),
    };
  };
}
