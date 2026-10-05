"""Looks up one real record per route for a role, so ZAP attacks real pages instead of
getting "not found" for every made-up id. Read-only: only GET requests are sent.
usage: real-examples.py <base url> <token> <manager|patient|crew>  (prints JSON)
"""
import json
import sys
import urllib.request

base, token, role = sys.argv[1].rstrip('/'), sys.argv[2], sys.argv[3]


def get(path):
    request = urllib.request.Request(base + '/api' + path, headers={'Authorization': f'Bearer {token}'})
    with urllib.request.urlopen(request, timeout=20) as response:
        return json.load(response)


def first(items, key='id'):
    return next((item[key] for item in items if item.get(key)), None)


examples = {'*': {'from': '2026-09-28', 'to': '2026-10-05', 'page': 1, 'pageSize': 20}}
if role == 'manager':
    calls = get('/emergency-calls?page=1&pageSize=50')['items']
    examples['/emergency-calls/{id}'] = {'id': first(calls)}
    examples['/dispatches/{id}'] = {'id': first(calls, 'active_dispatch_id')}
    examples['/dispatches/{id}/route'] = examples['/dispatches/{id}']
    ambulance = first(get('/ambulances?page=1&pageSize=50')['items'])
    examples['/ambulances/{id}'] = {'id': ambulance}
    examples['/ambulances/{id}/crew'] = {'id': ambulance}
    examples['/dispatch-proposals/{id}'] = {'id': first(get('/dispatch-proposals?page=1&pageSize=50')['items'])}
elif role == 'patient':
    examples['/me/emergency-calls/{id}/tracking'] = {'id': first(get('/me/emergency-calls?page=1&pageSize=50')['items'])}
elif role == 'crew':
    history = get('/me/dispatches/history?page=1&pageSize=50')['items']
    examples['/me/dispatches/{id}/navigation'] = {'id': first(history)}

print(json.dumps(examples))
