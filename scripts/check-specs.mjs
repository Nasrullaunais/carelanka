#!/usr/bin/env node
// Spec gate. The five specs describe ONE ASP.NET application, so routes,
// operationIds and schema names are global, not per-component.
//
//   - a duplicate route throws AmbiguousMatchException at startup, for everybody
//   - a duplicate operationId or schema name collides silently in the generated
//     clients, and whichever generates second wins
//
// Run: bun run check:specs   (or npm run check:specs)
//
// Note: `npx @apidevtools/swagger-parser` does not work - that package ships a
// library, not a CLI. Hence this script.

import { createRequire } from 'module';
import { readFileSync, readdirSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const require = createRequire(import.meta.url);
const yaml = require('js-yaml');
const SwaggerParser = require('@apidevtools/swagger-parser');

const specsDir = join(dirname(fileURLToPath(import.meta.url)), '..', 'specs');
const files = readdirSync(specsDir).filter((f) => f.endsWith('.yaml')).sort();

let failures = 0;
const fail = (msg) => { console.error('  FAIL ' + msg); failures++; };

console.log(`Checking ${files.length} specs in specs/\n`);

// ---------- 1. every spec is valid OpenAPI ----------
const docs = {};
for (const f of files) {
  try {
    docs[f] = yaml.load(readFileSync(join(specsDir, f), 'utf8'));
  } catch (e) {
    fail(`${f} is not parseable YAML: ${e.message.split('\n')[0]}`);
    continue;
  }
  try {
    await SwaggerParser.validate(join(specsDir, f));
    console.log(`  ok   ${f}`);
  } catch (e) {
    fail(`${f} is not valid OpenAPI: ${e.message.split('\n')[0]}`);
  }
}

// ---------- 2. collect the three global namespaces ----------
const routes = {};
const operationIds = {};
const schemas = {};
const add = (bag, key, value) => { (bag[key] ??= []).push(value); };

const METHODS = ['get', 'post', 'put', 'patch', 'delete', 'head', 'options'];

for (const [file, doc] of Object.entries(docs)) {
  if (!doc) continue;
  for (const [path, item] of Object.entries(doc.paths ?? {})) {
    for (const [method, op] of Object.entries(item ?? {})) {
      if (!METHODS.includes(method)) continue;
      add(routes, `${method.toUpperCase()} ${path}`, file);
      if (op?.operationId) add(operationIds, op.operationId, file);
    }
  }
  for (const [name, schema] of Object.entries(doc.components?.schemas ?? {})) {
    add(schemas, name, { file, body: JSON.stringify(schema) });
  }
}

// ---------- 3. routes and operationIds must be globally unique ----------
console.log('');
for (const [route, owners] of Object.entries(routes)) {
  if (owners.length > 1) fail(`duplicate route  ${route}  in ${owners.join(', ')}`);
}
for (const [id, owners] of Object.entries(operationIds)) {
  if (owners.length > 1) fail(`duplicate operationId  ${id}  in ${owners.join(', ')}`);
}

// ---------- 4. a shared schema name is fine ONLY if byte-identical ----------
// Two components needing different views of one thing give them different names
// (EquipmentBed / AdmissionBed), not one name and two shapes.
const shared = [];
for (const [name, defs] of Object.entries(schemas)) {
  if (defs.length < 2) continue;
  const distinct = new Set(defs.map((d) => d.body));
  if (distinct.size > 1) {
    fail(`schema "${name}" has ${distinct.size} different shapes across ` +
         defs.map((d) => d.file).join(', '));
  } else {
    shared.push(`${name} (${defs.length})`);
  }
}

// ---------- 5. report ----------
const pathCount = Object.keys(routes).length;
console.log(`  ${files.length} specs, ${pathCount} operations, ` +
            `${Object.keys(schemas).length} schema names`);
if (shared.length) console.log(`  shared and identical: ${shared.join(', ')}`);

if (failures > 0) {
  console.error(`\n${failures} problem${failures === 1 ? '' : 's'}. ` +
                `See integration_of_functions.md §11.6 for what was renamed and why.`);
  process.exit(1);
}
console.log('\nAll clear: 0 duplicate routes, 0 duplicate operationIds, 0 schema conflicts.');
