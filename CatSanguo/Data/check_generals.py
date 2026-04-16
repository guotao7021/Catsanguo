import json

with open('scenarios.json', 'r', encoding='utf-8-sig') as f:
    data = json.load(f)

for s in data:
    total = sum(len(f.get('initialGenerals', [])) for f in s['factions'])
    print(f"{s['id']}: {total} generals")
