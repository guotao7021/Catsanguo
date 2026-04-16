import json

with open('scenarios.json', 'r', encoding='utf-8-sig') as f:
    data = json.load(f)

print(f'Scenarios: {len(data)}')
for s in data:
    total_gens = 0
    total_cities = set()
    year = s.get('startDate', {}).get('year', '?')
    for faction in s.get('factions', []):
        for gen in faction.get('initialGenerals', []):
            total_gens += 1
            total_cities.add(gen.get('assignedCityId', 'unknown'))
    print(f'  {s["name"]} ({year}): {total_gens} generals in {len(total_cities)} cities')

print('All scenarios.json valid!')

with open('generals.json', 'r', encoding='utf-8-sig') as f:
    gens = json.load(f)
print(f'Generals: {len(gens)}')
ids = [g['id'] for g in gens]
print(f'Unique IDs: {len(set(ids))}')
print('All generals.json valid!')
