import json

print("=" * 60)
print("DATA INTEGRITY VERIFICATION")
print("=" * 60)

# Load all data
with open('CatSanguo/Data/generals.json', 'r', encoding='utf-8') as f:
    generals = json.load(f)
with open('CatSanguo/Data/skills.json', 'r', encoding='utf-8') as f:
    skills = json.load(f)
with open('CatSanguo/Data/scenarios.json', 'r', encoding='utf-8') as f:
    scenarios = json.load(f)
with open('CatSanguo/Data/cities.json', 'r', encoding='utf-8-sig') as f:
    cities = json.load(f)

city_ids = set(c['id'] for c in cities)
general_ids = set(g['id'] for g in generals)
skill_ids = set(s['id'] for s in skills)

errors = 0

# 1. General count
print(f"\n[1] Generals: {len(generals)}")

# 2. Unique general IDs
gen_id_list = [g['id'] for g in generals]
gen_dupes = set(x for x in gen_id_list if gen_id_list.count(x) > 1)
if gen_dupes:
    print(f"  ERROR: Duplicate general IDs: {gen_dupes}")
    errors += 1
else:
    print(f"  OK: All {len(generals)} general IDs unique")

# 3. Skill count
print(f"\n[2] Skills: {len(skills)}")
skill_id_list = [s['id'] for s in skills]
skill_dupes = set(x for x in skill_id_list if skill_id_list.count(x) > 1)
if skill_dupes:
    print(f"  WARNING: Duplicate skill IDs: {skill_dupes}")
else:
    print(f"  OK: All {len(skills)} skill IDs unique")

# 4. General -> Skill references
print(f"\n[3] General skill references:")
missing_active = []
missing_passive = []
for g in generals:
    if g['activeSkillId'] not in skill_ids:
        missing_active.append((g['id'], g['activeSkillId']))
    if g['passiveSkillId'] not in skill_ids:
        missing_passive.append((g['id'], g['passiveSkillId']))

if missing_active:
    print(f"  ERROR: {len(missing_active)} missing active skills: {missing_active[:5]}...")
    errors += len(missing_active)
else:
    print(f"  OK: All active skill references valid")

if missing_passive:
    print(f"  ERROR: {len(missing_passive)} missing passive skills: {missing_passive[:5]}...")
    errors += len(missing_passive)
else:
    print(f"  OK: All passive skill references valid")

# 5. General -> City references (check which ones are invalid)
print(f"\n[4] General city references:")
invalid_cities = [(g['id'], g['name'], g['appearCityId']) for g in generals if g['appearCityId'] not in city_ids]
if invalid_cities:
    print(f"  INFO: {len(invalid_cities)} generals have non-standard appearCityId (pre-existing)")
    # Check if any new generals have invalid cities
    new_invalid = [(gid, name, cid) for gid, name, cid in invalid_cities if gen_id_list.index(gid) >= 70]
    if new_invalid:
        print(f"  ERROR: {len(new_invalid)} NEW generals have invalid cities: {new_invalid}")
        errors += len(new_invalid)
    else:
        print(f"  OK: All NEW generals have valid city references")
else:
    print(f"  OK: All city references valid")

# 6. Formation validation
print(f"\n[5] Formation validation:")
valid_formations = {'cavalry', 'vanguard', 'archer'}
invalid_form = [(g['id'], g['preferredFormation']) for g in generals if g['preferredFormation'] not in valid_formations]
if invalid_form:
    print(f"  ERROR: Invalid formations: {invalid_form}")
    errors += len(invalid_form)
else:
    print(f"  OK: All formations valid")

# 7. Stat ranges
print(f"\n[6] Stat ranges:")
stat_fields = ['strength', 'intelligence', 'command', 'politics', 'speed', 'loyalty', 'charisma']
stat_errors = []
for g in generals:
    for field in stat_fields:
        val = g.get(field, 0)
        if val < 1 or val > 100:
            stat_errors.append((g['id'], field, val))
if stat_errors:
    print(f"  ERROR: {len(stat_errors)} stat range violations: {stat_errors[:5]}...")
    errors += len(stat_errors)
else:
    print(f"  OK: All stats within 1-100")

# 8. Scenario validation
print(f"\n[7] Scenario validation:")
scenario_errors = 0
for s in scenarios:
    year = s['startDate']['year']
    for faction in s['factions']:
        faction_cities = set(faction['initialCityIds'])
        for ga in faction['initialGenerals']:
            gid = ga['generalId']
            cid = ga['assignedCityId']
            # Check general exists
            if gid not in general_ids:
                print(f"  ERROR: {s['id']}/{faction['factionId']}: general {gid} not in generals.json")
                scenario_errors += 1
            # Check city is in faction
            if cid not in faction_cities:
                print(f"  ERROR: {s['id']}/{faction['factionId']}: city {cid} not in faction cities for {gid}")
                scenario_errors += 1
            # Check appearYear
            if gid in general_ids:
                gen = next(g for g in generals if g['id'] == gid)
                if gen['appearYear'] > year:
                    print(f"  WARNING: {s['id']}/{faction['factionId']}: {gid} appears in {gen['appearYear']} but scenario starts {year}")

if scenario_errors == 0:
    print(f"  OK: All scenario references valid")
else:
    errors += scenario_errors

# 9. Skill type distribution
print(f"\n[8] Skill distribution:")
active_count = sum(1 for s in skills if s['type'] == 'active')
passive_count = sum(1 for s in skills if s['type'] == 'passive')
print(f"  Active: {active_count}, Passive: {passive_count}")

# 10. Summary
print(f"\n{'=' * 60}")
print(f"SUMMARY")
print(f"{'=' * 60}")
print(f"Generals: {len(generals)}")
print(f"Skills: {len(skills)} (active: {active_count}, passive: {passive_count})")
print(f"Scenarios: {len(scenarios)}")
print(f"Cities: {len(cities)}")
print(f"Total errors: {errors}")
if errors == 0:
    print("ALL CHECKS PASSED!")
else:
    print(f"FOUND {errors} ERRORS - NEEDS FIXING")
