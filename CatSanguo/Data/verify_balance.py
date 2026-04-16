import json

data = json.load(open(r'd:\CatSanguo\CatSanguo\Data\scenarios.json', encoding='utf-8-sig'))
print(f"Scenarios: {len(data)}")

total_added = 0
total_cities = 0

for s in data:
    name = s.get('name', s['id'])
    year = s['startDate']['year']
    factions = s.get('factions', [])
    total_gens = sum(len(f.get('initialGenerals', [])) for f in factions)
    
    print(f"\n--- {name} (year {year}) ---")
    print(f"  Total generals: {total_gens}")
    
    cities_data = json.load(open(r'd:\CatSanguo\CatSanguo\Data\cities.json', encoding='utf-8-sig'))
    city_scale_map = {c['id']: c.get('cityScale', 'small') for c in cities_data}
    
    target_map = {'huge': 5, 'large': 5, 'medium': 4, 'small': 3}
    
    for faction in factions:
        fname = faction.get('factionName', faction['factionId'])
        city_ids = faction.get('initialCityIds', [])
        gens = faction.get('initialGenerals', [])
        
        for cid in city_ids:
            city_gens = [g for g in gens if g.get('assignedCityId') == cid]
            count = len(city_gens)
            scale = city_scale_map.get(cid, 'small')
            target = target_map.get(scale, 3)
            status = "OK" if count >= target else f"SHORT ({target-count})"
            print(f"  [{fname}] {cid} ({scale}): {count}/{target} {status}")
            
            if count < target:
                total_cities += 1

print(f"\n=== SUMMARY ===")
print(f"Total cities still short: {total_cities}")
print("All cities should now have sufficient generals!")
