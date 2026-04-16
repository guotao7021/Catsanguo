import json
import random

random.seed(42)

# Read inputs
with open('CatSanguo/Data/generals.json', 'r', encoding='utf-8') as f:
    generals = json.load(f)

with open('CatSanguo/Data/skills.json', 'r', encoding='utf-8-sig') as f:
    existing_skills = json.load(f)

new_generals = generals[70:]

# Buff types for passive skills, distributed cyclically
buff_types = ['attack', 'defense', 'crit', 'dodge', 'regen', 'skill_damage', 'morale_floor']

# Tier config
tier_config = {
    'S': {'coeff_range': (0.40, 0.60), 'buff_range': (0.15, 0.25), 'cooldown_range': (3, 4), 'radius': 180},
    'A': {'coeff_range': (0.25, 0.40), 'buff_range': (0.10, 0.18), 'cooldown_range': (2, 3), 'radius': 150},
    'B': {'coeff_range': (0.15, 0.30), 'buff_range': (0.05, 0.12), 'cooldown_range': (2, 3), 'radius': 120},
    'C': {'coeff_range': (0.10, 0.20), 'buff_range': (0.05, 0.08), 'cooldown_range': (2, 2), 'radius': 100},
}

# Active skill name patterns (Chinese)
warrior_active_names = [
    "猛击", "横斩", "冲锋", "突袭", "破阵", "奋战", "猛冲", "斩击",
    "烈斩", "怒击", "战吼", "铁壁", "突破", "冲阵", "猛攻", "追击",
    "劈斩", "疾突", "破甲", "断刃", "重击", "震击", "暴击", "裂击",
    "穿刺", "狂斩", "乱斩", "旋风", "碎击", "铁拳", "猛袭", "猛刺"
]

strategist_active_names = [
    "火计", "水计", "落石", "毒计", "伏兵", "奇谋", "妙计", "神算",
    "连环", "反间", "迷雾", "天罚", "雷击", "冰封", "暗算", "绝策",
    "离间", "惑敌", "诱敌", "心战", "幻术", "业火", "冰箭", "风暴",
    "雷鸣", "星落", "玄策", "天谴", "地裂", "灵光", "幻影", "魔击"
]

# Passive skill buff name mapping (Chinese)
buff_names = {
    'attack': ["攻击强化", "武力觉醒", "攻势增幅", "力量激发", "战意昂扬", "杀意凝聚", "破敌之志"],
    'defense': ["防御强化", "铁壁之心", "坚守意志", "不动如山", "护盾之力", "刚毅之躯", "盾卫之魂"],
    'crit': ["会心一击", "致命洞察", "破绽捕捉", "锐眼如鹰", "必杀之势", "一击必中", "闪电精准"],
    'dodge': ["灵巧闪避", "风行之体", "疾风步法", "身轻如燕", "鬼魅身法", "闪电步伐", "影遁之术"],
    'regen': ["生命恢复", "不屈之躯", "战场回春", "久战不衰", "铁人之体", "战后修养", "续战之能"],
    'skill_damage': ["技能强化", "术法精通", "智谋增幅", "计略加成", "策略大师", "奇策增幅", "谋略之光"],
    'morale_floor': ["士气维持", "军心凝聚", "鼓舞士气", "不屈军魂", "战意不灭", "铁军之魂", "坚定信念"],
}

# Target mode options for active skills
warrior_target_modes = ['AOE_Circle', 'Single_Enemy', 'AOE_Circle', 'Single_Enemy']
strategist_target_modes = ['AOE_Circle', 'AOE_Circle', 'Single_Enemy', 'All_Allies']

new_skills = []
buff_idx = 0

for i, g in enumerate(new_generals):
    is_warrior = g['strength'] >= g['intelligence']
    mx = max(g['strength'], g['intelligence'], g['command'])
    if mx >= 88:
        tier = 'S'
    elif mx >= 75:
        tier = 'A'
    elif mx >= 60:
        tier = 'B'
    else:
        tier = 'C'
    
    cfg = tier_config[tier]
    
    # Generate active skill
    coeff = round(random.uniform(*cfg['coeff_range']), 2)
    cooldown = random.randint(*cfg['cooldown_range'])
    
    if is_warrior:
        stat_basis = random.choice(['strength', 'command'])
        target_mode = random.choice(warrior_target_modes)
        name_pool = warrior_active_names
        effect_type = 'damage'
    else:
        stat_basis = 'intelligence'
        target_mode = random.choice(strategist_target_modes)
        name_pool = strategist_active_names
        # Some strategists get morale effects
        if random.random() < 0.15:
            effect_type = 'morale'
        else:
            effect_type = 'damage'
    
    active_name_idx = i % len(name_pool)
    active_cn_name = g['name'] + name_pool[active_name_idx]
    
    active_skill = {
        'id': g['activeSkillId'],
        'name': active_cn_name,
        'description': active_cn_name + '，对敌方造成伤害',
        'type': 'active',
        'targetMode': target_mode,
        'coefficient': coeff,
        'radius': cfg['radius'] if 'AOE' in target_mode else 0,
        'cooldown': cooldown,
        'castTime': round(random.uniform(0.3, 0.8), 1),
        'effectType': effect_type,
        'statBasis': stat_basis,
        'buffStat': '',
        'buffPercent': 0,
        'buffDuration': 0,
        'moraleChange': random.randint(-15, -8) if effect_type == 'morale' else 0,
        'triggers': []
    }
    new_skills.append(active_skill)
    
    # Generate passive skill
    buff_type = buff_types[buff_idx % len(buff_types)]
    buff_idx += 1
    buff_pct = round(random.uniform(*cfg['buff_range']), 2)
    
    # Pick a Chinese name for the passive
    passive_name_pool = buff_names[buff_type]
    passive_cn_name = passive_name_pool[i % len(passive_name_pool)]
    
    passive_skill = {
        'id': g['passiveSkillId'],
        'name': passive_cn_name,
        'description': g['name'] + '的被动能力：' + passive_cn_name,
        'type': 'passive',
        'targetMode': 'Self',
        'coefficient': 1.0,
        'radius': 0,
        'cooldown': 0,
        'castTime': 0,
        'effectType': 'buff',
        'statBasis': stat_basis if is_warrior else 'intelligence',
        'buffStat': buff_type,
        'buffPercent': buff_pct,
        'buffDuration': 0,
        'moraleChange': 0,
        'triggers': []
    }
    new_skills.append(passive_skill)

# Merge with existing
all_skills = existing_skills + new_skills

# Verify no duplicate IDs
skill_ids = [s['id'] for s in all_skills]
dupes = set([x for x in skill_ids if skill_ids.count(x) > 1])
if dupes:
    print(f"WARNING: Duplicate skill IDs: {dupes}")
else:
    print("All skill IDs unique")

print(f"Existing skills: {len(existing_skills)}")
print(f"New skills: {len(new_skills)}")
print(f"Total skills: {len(all_skills)}")

# Write output
with open('CatSanguo/Data/skills.json', 'w', encoding='utf-8') as f:
    json.dump(all_skills, f, ensure_ascii=False, indent=2)

print("skills.json written successfully")

# Verify all general skill references are valid
with open('CatSanguo/Data/generals.json', 'r', encoding='utf-8') as f:
    all_generals = json.load(f)

skill_id_set = set(skill_ids)
missing_active = []
missing_passive = []
for g in all_generals:
    if g['activeSkillId'] not in skill_id_set:
        missing_active.append((g['id'], g['activeSkillId']))
    if g['passiveSkillId'] not in skill_id_set:
        missing_passive.append((g['id'], g['passiveSkillId']))

if missing_active:
    print(f"Missing active skills: {missing_active}")
else:
    print("All active skill references valid")

if missing_passive:
    print(f"Missing passive skills: {missing_passive}")
else:
    print("All passive skill references valid")
