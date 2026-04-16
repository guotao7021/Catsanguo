import json

with open('CatSanguo/Data/generals.json', 'r', encoding='utf-8') as f:
    generals = json.load(f)

new_generals = generals[70:]
output = []
for g in new_generals:
    is_w = g['strength'] >= g['intelligence']
    mx = max(g['strength'], g['intelligence'], g['command'])
    if mx >= 88:
        tier = 'S'
    elif mx >= 75:
        tier = 'A'
    elif mx >= 60:
        tier = 'B'
    else:
        tier = 'C'
    output.append({
        'id': g['id'],
        'name': g['name'],
        'activeSkillId': g['activeSkillId'],
        'passiveSkillId': g['passiveSkillId'],
        'is_warrior': is_w,
        'tier': tier,
        'strength': g['strength'],
        'intelligence': g['intelligence'],
        'command': g['command']
    })

with open('skill_gen_input.json', 'w', encoding='utf-8') as f:
    json.dump(output, f, ensure_ascii=False)

tiers = {}
for o in output:
    t = o['tier']
    tiers[t] = tiers.get(t, 0) + 1

warriors = sum(1 for o in output if o['is_warrior'])
strategists = len(output) - warriors

print("Tiers:", tiers)
print("Warriors:", warriors, "Strategists:", strategists)
print("Total:", len(output))
