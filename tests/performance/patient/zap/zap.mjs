import { appendFileSync, existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const resultsDir = join(here, '..', 'results');
const outDir = join(resultsDir, 'zap');
const specPath = join(here, '..', '..', '..', '..', 'specs', 'patient-spec.yaml');

const ZAP_URL = (process.env.ZAP_URL || 'http://127.0.0.1:8080').replace(/\/+$/, '');
const RULE_DESCRIPTION = 'QM-ZAP-bearer';
const GUID = '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}';

const NOT_SCANNED = new Set([
  '/me/care-queries',
  '/care-recommendations/{id}/redraft',
  '/admissions/pre-admit',
]);
const METHODS = ['get', 'post', 'put', 'patch', 'delete'];
const SCOPE_PATTERN =
  'patients|patient-accounts|admissions|discharges|appointments|wards|capacity|bed-availability|' +
  'billing|patient-worklist|care-recommendations|care-workflows|me|beds/[^/]+/occupancy';

function requireEnv(name) {
  const value = process.env[name];
  if (!value) {
    throw new Error(`${name} is not set in this session.`);
  }
  return value;
}

const BASE = requireEnv('BASE_URL').replace(/\/+$/, '');

async function api(method, path, { token, body } = {}) {
  const headers = { 'Content-Type': 'application/json' };
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  const res = await fetch(`${BASE}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  let json = null;
  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    json = null;
  }
  return { status: res.status, json };
}

async function login(who) {
  const result =
    who === 'staff'
      ? await api('POST', '/api/auth/login', {
          body: { email: requireEnv('QM_STAFF_USER'), password: requireEnv('QM_STAFF_PASSWORD') },
        })
      : await api('POST', '/api/auth/patient/login', {
          body: { username: requireEnv('QM_PATIENT_USER'), password: requireEnv('QM_PATIENT_PASSWORD') },
        });
  if (result.status !== 200 || !result.json?.access_token) {
    throw new Error(`The ${who} login returned HTTP ${result.status}. Nothing was changed.`);
  }
  return result.json.access_token;
}

function expiryOf(token) {
  const payload = JSON.parse(Buffer.from(token.split('.')[1], 'base64url').toString('utf8'));
  return new Date(payload.exp * 1000);
}

function clock(date) {
  return date.toLocaleTimeString('en-GB', { hour12: false });
}

function stripApiPrefix(path) {
  return path.replace(/^\/api(?=\/)/, '');
}

function readSpecPaths() {
  const lines = readFileSync(specPath, 'utf8').split(/\r?\n/);
  return new Set(lines.map((line) => /^ {2}(\/\S+):\s*$/.exec(line)?.[1]).filter(Boolean));
}

function readLogs() {
  if (!existsSync(resultsDir)) {
    return [];
  }
  return readdirSync(resultsDir)
    .filter((name) => /^created-ids-.*\.log$/.test(name))
    .sort()
    .reverse()
    .map((name) => ({ name, text: readFileSync(join(resultsDir, name), 'utf8') }));
}

function findOwnPatientId() {
  const pattern = new RegExp(`QM_CREATED patient (${GUID})`);
  for (const log of readLogs().filter((l) => l.name.startsWith('created-ids-setup-'))) {
    const match = pattern.exec(log.text);
    if (match) {
      return match[1];
    }
  }
  throw new Error('The throwaway patient id was not found in a created-ids-setup log.');
}

function findSpareBooking() {
  const pattern = new RegExp(`QM_CREATED appointment (${GUID}) for patient (${GUID})`);
  for (const log of readLogs()) {
    const match = pattern.exec(log.text);
    if (match) {
      return { appointmentId: match[1], patientId: match[2] };
    }
  }
  throw new Error('No QM Test appointment was found in the k6 created-ids logs.');
}

function exampleFor(path, name, ids) {
  if (path.startsWith('/patient-accounts/') || /^\/patients\/\{id\}/.test(path)) {
    return ids.spare_patient_id;
  }
  if (name === 'admissionId' || (name === 'id' && path.includes('/admissions/'))) {
    return ids.admission_id;
  }
  if (name === 'appointmentId' || (name === 'id' && path.includes('/appointments/'))) {
    return ids.appointment_id;
  }
  return undefined;
}

export function buildDocs(doc, specPaths, ids) {
  const kept = { staff: {}, me: {} };
  const leftOut = [];
  const outsideSpec = [];
  const unmapped = new Set();

  for (const [rawPath, item] of Object.entries(doc.paths)) {
    const path = stripApiPrefix(rawPath);
    if (!specPaths.has(path)) {
      if (new RegExp(`^/(?:${SCOPE_PATTERN})(?:/|$)`).test(path)) {
        outsideSpec.push(path);
      }
      continue;
    }
    if (NOT_SCANNED.has(path)) {
      leftOut.push(path);
      continue;
    }

    const copy = structuredClone(item);
    const lists = [copy.parameters, ...METHODS.map((m) => copy[m]?.parameters)];
    for (const list of lists) {
      for (const param of list ?? []) {
        if (param.in !== 'path') {
          continue;
        }
        const value = exampleFor(path, param.name, ids);
        if (value) {
          param.example = value;
          if (param.schema) {
            param.schema.example = value;
          }
        } else {
          unmapped.add(`${path} {${param.name}}`);
        }
      }
    }
    kept[path.startsWith('/me/') ? 'me' : 'staff'][rawPath] = copy;
  }

  const wrap = (paths) => {
    const { servers, ...rest } = doc;
    return { ...rest, paths };
  };
  return {
    staff: wrap(kept.staff),
    me: wrap(kept.me),
    leftOut,
    outsideSpec,
    unmapped: [...unmapped],
    missingFromLive: [...specPaths].filter(
      (p) => !Object.keys(doc.paths).some((raw) => stripApiPrefix(raw) === p),
    ),
  };
}

function operationCount(doc) {
  return Object.values(doc.paths).reduce(
    (sum, item) => sum + METHODS.filter((m) => item[m]).length,
    0,
  );
}

function escapeRegex(text) {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

async function seedAdmission(staffToken, ownPatientId, stamp) {
  const idsFile = join(outDir, 'zap-ids.json');
  const known = existsSync(idsFile) ? JSON.parse(readFileSync(idsFile, 'utf8')) : {};

  if (known.admission_id && known.own_patient_id === ownPatientId) {
    const check = await api('GET', `/api/admissions/${known.admission_id}`, { token: staffToken });
    if (check.status === 200) {
      return known.admission_id;
    }
  }

  const created = await api('POST', '/api/admissions', {
    token: staffToken,
    body: {
      patient_id: ownPatientId,
      source: 'walk_in',
      admission_category: 'general',
      urgency: 'routine',
      is_infectious: false,
    },
  });
  if (created.status !== 201) {
    throw new Error(
      `Creating the QM Test admission returned HTTP ${created.status}${created.json?.code ? ` (${created.json.code})` : ''}.`,
    );
  }
  const line = `QM_CREATED admission ${created.json.id} for patient ${ownPatientId}`;
  console.log(line);
  appendFileSync(join(resultsDir, `created-ids-zap-prepare-${stamp}.log`), `${line}\n`);
  return created.json.id;
}

async function prepare() {
  mkdirSync(outDir, { recursive: true });
  const now = new Date();
  const stamp = new Date(now.getTime() - now.getTimezoneOffset() * 60000)
    .toISOString()
    .replace(/[:T]/g, (c) => (c === 'T' ? '_' : '-'))
    .slice(0, 19);

  const health = await api('GET', '/api/health');
  if (health.status !== 200 || health.json?.database !== 'up') {
    throw new Error(`Health check failed (HTTP ${health.status}). Nothing was changed.`);
  }
  console.log('Health check: database up.');

  const swagger = await api('GET', '/swagger/v1/swagger.json');
  if (swagger.status !== 200 || !swagger.json?.paths) {
    throw new Error(
      `The live API description is not served (HTTP ${swagger.status}). Stopping. The fallback is specs/patient-spec.yaml, which is a decision for the user.`,
    );
  }
  console.log(`Live API description: ${Object.keys(swagger.json.paths).length} paths.`);

  const staffToken = await login('staff');
  const ownPatientId = findOwnPatientId();
  const spare = findSpareBooking();

  for (const [label, id] of [['throwaway patient', ownPatientId], ['spare patient', spare.patientId]]) {
    const check = await api('GET', `/api/patients/${id}`, { token: staffToken });
    if (check.status !== 200) {
      throw new Error(`The ${label} record could not be read (HTTP ${check.status}).`);
    }
  }

  const admissionId = await seedAdmission(staffToken, ownPatientId, stamp);
  const ids = {
    own_patient_id: ownPatientId,
    admission_id: admissionId,
    spare_patient_id: spare.patientId,
    appointment_id: spare.appointmentId,
  };
  writeFileSync(join(outDir, 'zap-ids.json'), JSON.stringify(ids, null, 2));

  const built = buildDocs(swagger.json, readSpecPaths(), ids);
  writeFileSync(join(outDir, 'staff-routes.openapi.json'), JSON.stringify(built.staff, null, 2));
  writeFileSync(join(outDir, 'me-routes.openapi.json'), JSON.stringify(built.me, null, 2));

  console.log('');
  console.log(`staff-routes.openapi.json: ${Object.keys(built.staff.paths).length} paths, ${operationCount(built.staff)} operations`);
  console.log(`me-routes.openapi.json:    ${Object.keys(built.me.paths).length} paths, ${operationCount(built.me)} operations`);
  console.log(`Left out on purpose:       ${built.leftOut.join(', ') || 'none found'}`);
  console.log(`Path ids still generated:  ${built.unmapped.join('; ') || 'none'}`);
  console.log(`Patient-looking paths in the live document but not in patient-spec.yaml (not scanned): ${built.outsideSpec.join(', ') || 'none'}`);
  console.log(`In patient-spec.yaml but not in the live document: ${built.missingFromLive.join(', ') || 'none'}`);

  const prefixed = Object.keys(swagger.json.paths).every((p) => p.startsWith('/api/'));
  console.log('');
  console.log(`ZAP import target (type this in the import dialog): ${prefixed ? BASE : `${BASE}/api`}`);

  const host = escapeRegex(BASE);
  console.log('');
  console.log('ZAP context, include in context:');
  console.log(`  ^${host}/api/(?:${SCOPE_PATTERN})(?:[/?].*)?$`);
  console.log('ZAP context, exclude from context:');
  console.log(`  ^${host}/api/auth/.*`);
  console.log(`  ^${host}/api/me/care-queries.*`);
  console.log(`  ^${host}/api/care-recommendations/[^/]+/redraft.*`);
  console.log(`  ^${host}/api/admissions/pre-admit.*`);
}

async function zapCall(path, params, { allowMissing = false } = {}) {
  let res;
  try {
    res = await fetch(`${ZAP_URL}/JSON/${path}/?${new URLSearchParams(params)}`);
  } catch {
    throw new Error(
      `ZAP is not reachable at ${ZAP_URL}. Start it with: zap.bat -config api.disablekey=true`,
    );
  }
  const json = await res.json().catch(() => ({}));
  if (!res.ok && !(allowMissing && json.code === 'does_not_exist')) {
    throw new Error(`ZAP refused ${path}: ${json.code ?? res.status} ${json.message ?? ''}`.trim());
  }
  return json;
}

async function token(who) {
  if (who === 'clear') {
    await zapCall('replacer/action/removeRule', { description: RULE_DESCRIPTION }, { allowMissing: true });
    console.log('ZAP rule removed. Requests now go out without a token.');
    return;
  }
  if (who !== 'staff' && who !== 'patient') {
    throw new Error('Use: token staff, token patient or token clear.');
  }

  const accessToken = await login(who);
  await zapCall('replacer/action/removeRule', { description: RULE_DESCRIPTION }, { allowMissing: true });
  await zapCall('replacer/action/addRule', {
    description: RULE_DESCRIPTION,
    enabled: 'true',
    matchType: 'REQ_HEADER',
    matchRegex: 'false',
    matchString: 'Authorization',
    replacement: `Bearer ${accessToken}`,
    initiators: '',
  });

  const rules = await zapCall('replacer/view/rules', {});
  if (Array.isArray(rules.rules)) {
    const rule = rules.rules.find((r) => r.description === RULE_DESCRIPTION);
    if (!rule || String(rule.enabled) !== 'true') {
      throw new Error('The rule was not found enabled in ZAP after adding it.');
    }
  }

  const probe = who === 'staff' ? '/api/wards' : '/api/me/profile';
  const check = spawnSync(
    'curl.exe',
    ['-k', '-s', '-o', 'NUL', '-w', '%{http_code}', '-x', ZAP_URL, `${BASE}${probe}`],
    { encoding: 'utf8' },
  );
  const code = (check.stdout || '').trim();

  console.log(`ZAP now sends the ${who} token. It expires at ${clock(expiryOf(accessToken))}.`);
  console.log(`Check through ZAP (${probe}): HTTP ${code || 'no answer'} ${code === '200' ? '(ok)' : '(NOT ok, do not start a scan)'}`);
  if (code !== '200') {
    process.exitCode = 1;
  }
}

const CONTEXT_NAME = 'QM-Patient';

async function waitFor(label, read, done, { seconds = 5, limitMs = Infinity } = {}) {
  const end = Date.now() + limitMs;
  for (;;) {
    const value = await read();
    if (done(value) || Date.now() >= end) {
      return value;
    }
    console.log(`  ${label}: ${value}`);
    await new Promise((r) => setTimeout(r, seconds * 1000));
  }
}

async function setUpContext() {
  const host = escapeRegex(BASE);
  const contexts = (await zapCall('context/view/contextList', {})).contextList ?? [];
  if (contexts.includes(CONTEXT_NAME)) {
    await zapCall('context/action/removeContext', { contextName: CONTEXT_NAME });
  }
  const { contextId } = await zapCall('context/action/newContext', { contextName: CONTEXT_NAME });
  await zapCall('context/action/includeInContext', {
    contextName: CONTEXT_NAME,
    regex: `^${host}/api/(?:${SCOPE_PATTERN})(?:[/?].*)?$`,
  });
  for (const regex of [
    `^${host}/api/auth/.*`,
    `^${host}/api/me/care-queries.*`,
    `^${host}/api/care-recommendations/[^/]+/redraft.*`,
    `^${host}/api/admissions/pre-admit.*`,
  ]) {
    await zapCall('context/action/excludeFromContext', { contextName: CONTEXT_NAME, regex });
  }
  await zapCall('context/action/setContextInScope', { contextName: CONTEXT_NAME, booleanInScope: 'true' });
  await zapCall('core/action/setMode', { mode: 'protect' });
  console.log(`ZAP context ${CONTEXT_NAME} set, Protected mode on.`);
  return contextId;
}

async function scan(who, minutes) {
  if (who !== 'staff' && who !== 'patient') {
    throw new Error('Use: scan staff or scan patient.');
  }
  const scanMinutes = Number(minutes) || 3;
  const file = join(outDir, who === 'staff' ? 'staff-routes.openapi.json' : 'me-routes.openapi.json');
  if (!existsSync(file)) {
    throw new Error(`${file} is missing. Run prepare first.`);
  }
  const routes = JSON.parse(readFileSync(file, 'utf8'));
  const target = Object.keys(routes.paths).every((p) => p.startsWith('/api/')) ? BASE : `${BASE}/api`;

  const contextId = await setUpContext();
  await token(who);
  if (process.exitCode === 1) {
    throw new Error('The token check through ZAP failed. No scan was started.');
  }

  console.log('');
  console.log(`Passive scan: importing ${Object.keys(routes.paths).length} ${who} addresses into ZAP.`);
  await zapCall('openapi/action/importFile', { file, target, contextId });
  await waitFor(
    'Requests ZAP still has to read',
    async () => Number((await zapCall('pscan/view/recordsToScan', {})).recordsToScan),
    (left) => left === 0,
  );
  console.log('Passive scan finished.');

  const start = who === 'staff' ? `${BASE}/api/patients` : `${BASE}/api/me`;
  await zapCall('ascan/action/setOptionThreadPerHost', { Integer: '2' });
  await zapCall('ascan/action/setOptionDelayInMs', { Integer: '100' });
  await zapCall('ascan/action/setOptionMaxScanDurationInMins', { Integer: String(scanMinutes) });
  console.log('');
  console.log(`Active scan: attacking ${start} for at most ${scanMinutes} minutes.`);
  const { scan: scanId } = await zapCall('ascan/action/scan', { url: start, recurse: 'true', contextId });
  await waitFor(
    'Active scan progress %',
    async () => Number((await zapCall('ascan/view/status', { scanId })).status),
    (progress) => progress >= 100,
    { seconds: 15, limitMs: (scanMinutes + 1) * 60000 },
  );
  await zapCall('ascan/action/stop', { scanId }, { allowMissing: true });
  const sent = (await zapCall('ascan/view/messagesIds', { scanId })).messagesIds ?? [];
  console.log(`Active scan stopped. Attack requests sent: ${sent.length}.`);

  await token('clear');

  const alerts = (await zapCall('alert/view/alerts', { baseurl: BASE })).alerts ?? [];
  const byName = new Map();
  for (const a of alerts) {
    const key = `${a.risk}: ${a.alert}`;
    byName.set(key, (byName.get(key) ?? 0) + 1);
  }
  console.log('');
  console.log(`Alerts found: ${byName.size} kind(s)`);
  for (const [key, pages] of byName) {
    console.log(`  ${key} (on ${pages} page${pages === 1 ? '' : 's'})`);
  }

  const now = new Date();
  const stamp = new Date(now.getTime() - now.getTimezoneOffset() * 60000)
    .toISOString()
    .replace(/[:T]/g, (c) => (c === 'T' ? '_' : '-'))
    .slice(0, 19);
  const report = join(outDir, `zap-report-${who}-${stamp}.html`);
  const html = await fetch(`${ZAP_URL}/OTHER/core/other/htmlreport/`);
  writeFileSync(report, await html.text());
  console.log(`Report saved: ${report}`);
}

async function main() {
  const [command, who, minutes] = process.argv.slice(2);
  if (command === 'prepare') {
    await prepare();
  } else if (command === 'token') {
    await token(who);
  } else if (command === 'scan') {
    await scan(who, minutes);
  } else {
    throw new Error('Use: prepare, token staff, token patient, token clear, scan staff or scan patient.');
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main().catch((error) => {
    console.error(error.message);
    process.exit(1);
  });
}
