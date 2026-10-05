"""Keeps only the Emergency read routes for one role from the live API description.
usage: emergency-routes.py <swagger.json> <manager|patient|crew> <out.json> <server url> [examples.json]
"""
import json
import sys

source, role, out, server = sys.argv[1:5]
examples = json.load(open(sys.argv[5])) if len(sys.argv) > 5 else {}
spec = json.load(open(source))

MANAGER_TAGS = {'Ambulances', 'Calls', 'Cancellation Review', 'Dispatch Agent', 'Dispatches', 'Reports'}
# Address search forwards every request to the public OpenStreetMap service, so it is never scanned.
SKIPPED = {'/emergency-calls/address-search'}


def wanted(path, operation):
    if path in SKIPPED:
        return False
    if role == 'manager':
        if path.startswith('/reports/'):
            return path.startswith('/reports/emergency/')
        return operation.get('tags', [''])[0] in MANAGER_TAGS and not path.startswith('/me/') and path != '/ambulances/mine'
    if role == 'patient':
        return path.startswith('/me/emergency-calls')
    if role == 'crew':
        return path.startswith('/me/dispatches') or path == '/ambulances/mine'
    raise SystemExit(f'Unknown role {role}')


paths = {}
for path, operations in spec['paths'].items():
    reads = {method: op for method, op in operations.items() if method == 'get' and wanted(path, op)}
    if reads:
        paths[path] = reads

# ZAP starts its attacks from a parameter's example, so real values reach real records.
for path, operations in paths.items():
    for operation in operations.values():
        for parameter in operation.get('parameters', []):
            value = examples.get(path, {}).get(parameter['name'], examples.get('*', {}).get(parameter['name']))
            if value is not None:
                parameter['example'] = value

spec['paths'] = paths
spec['servers'] = [{'url': server.rstrip('/') + '/api'}]
json.dump(spec, open(out, 'w'), indent=1)
print(f'{role}: {len(paths)} routes')
for path in sorted(paths):
    print(f'  GET {path}')
