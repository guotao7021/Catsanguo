import json

data = json.load(open('wei_generals_60.json', 'r', encoding='utf-8'))
print(f"Total generals: {len(data)}")

ids = [g['id'] for g in data]
actives = [g['activeSkillId'] for g in data]
passives = [g['passiveSkillId'] for g in data]

print(f"Unique IDs: {len(set(ids))}")
print(f"Unique activeSkillIds: {len(set(actives))}")
print(f"Unique passiveSkillIds: {len(set(passives))}")

# Check for duplicate IDs
if len(ids) != len(set(ids)):
    from collections import Counter
    dupes = [k for k, v in Counter(ids).items() if v > 1]
    print(f"DUPLICATE IDs: {dupes}")

# Check valid cities
cities = set(g['appearCityId'] for g in data)
valid_cities = {
    'cheng_du','zi_tong','mian_zhu','jian_ge','han_zhong','bai_di','jiang_zhou',
    'yi_ling','chang_an','tong_guan','tian_shui','wu_wei','luo_yang','hu_lao',
    'xu_chang','ye_cheng','chen_liu','wan_cheng','pu_yang','ru_nan','nan_yang',
    'jin_yang','hu_guan','you_zhou','bei_ping','ping_yuan','xu_zhou','xia_pi',
    'xiao_pei','shou_chun','he_fei','jian_ye','chai_sang','wu_jun','kuai_ji',
    'lu_jiang','shan_yin','xiang_yang','jiang_ling','jiang_xia','chang_sha',
    'ling_ling','wu_ling','gui_yang'
}
invalid = cities - valid_cities
print(f"Invalid cities: {invalid if invalid else 'None'}")

# Check active/passive overlap
overlap = set(actives) & set(passives)
print(f"Active/Passive skill overlap: {overlap if overlap else 'None'}")

# Check stat ranges
for g in data:
    for stat in ['strength','intelligence','command','politics','speed','loyalty','charisma']:
        v = g[stat]
        if v < 1 or v > 100:
            print(f"OUT OF RANGE: {g['id']}.{stat} = {v}")
    if g['salary'] < 15 or g['salary'] > 50:
        print(f"SALARY OUT OF RANGE: {g['id']}.salary = {g['salary']}")
    if g['preferredFormation'] not in ('cavalry','vanguard','archer'):
        print(f"INVALID FORMATION: {g['id']}.preferredFormation = {g['preferredFormation']}")

# Check existing generals not duplicated
existing = {'caocao','zhangliao','xiahoudun','xiahouyuan','caoren','xuhuang',
            'zhanghe','dianwei','xuchu','lidian','yujin','caohong','caopi',
            'simayi','xunyu','guojia','chengyu','jiaxu','liudian'}
conflicts = existing & set(ids)
print(f"Conflicts with existing: {conflicts if conflicts else 'None'}")

print("\nAll checks passed!" if not invalid and not overlap and not conflicts else "")
