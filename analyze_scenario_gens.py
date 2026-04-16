import json

with open('CatSanguo/Data/scenarios.json', 'r', encoding='utf-8') as f:
    scenarios = json.load(f)
with open('CatSanguo/Data/generals.json', 'r', encoding='utf-8') as f:
    generals = json.load(f)

gen_map = {g['id']: g for g in generals}

for s in scenarios:
    print(f"\n=== {s['name']} ({s['startDate']['year']}) ===")
    for faction in s['factions']:
        print(f"  [{faction['factionName']}] cities: {len(faction['initialCityIds'])}, generals: {len(faction['initialGenerals'])}")
        city_gens = {}
        for ga in faction['initialGenerals']:
            cid = ga['assignedCityId']
            if cid not in city_gens:
                city_gens[cid] = []
            city_gens[cid].append(ga['generalId'])
        for cid, gids in city_gens.items():
            names = [gen_map[gid]['name'] if gid in gen_map else gid for gid in gids]
            suffix = "..." if len(names) > 3 else ""
            print(f"    {cid}: {len(gids)} gen(s) - {names[:3]}{suffix}")
