import json

with open('CatSanguo/Data/generals.json', 'r', encoding='utf-8') as f:
    generals = json.load(f)
with open('CatSanguo/Data/scenarios.json', 'r', encoding='utf-8-sig') as f:
    scenarios = json.load(f)

# Build general lookup
gen_map = {g['id']: g for g in generals}

# Define faction assignments for each scenario
# Format: {scenario_id: {faction_id: [(generalId, cityId, level, loyalty), ...]}}

# Helper: find faction in scenario
def add_generals_to_faction(scenario, faction_id, new_generals_list):
    for faction in scenario['factions']:
        if faction['factionId'] == faction_id:
            existing_ids = set(g['generalId'] for g in faction['initialGenerals'])
            cities = faction['initialCityIds']
            for gen_id, city_id, level, loyalty in new_generals_list:
                if gen_id in existing_ids:
                    continue
                if gen_id not in gen_map:
                    print(f"WARNING: {gen_id} not found in generals")
                    continue
                g = gen_map[gen_id]
                if g['appearYear'] > scenario['startDate']['year']:
                    continue
                if city_id not in cities:
                    city_id = cities[0]  # fallback to capital
                faction['initialGenerals'].append({
                    'generalId': gen_id,
                    'assignedCityId': city_id,
                    'initialLevel': level,
                    'initialLoyalty': loyalty
                })
            return

# ===== Scenario 1: huangjin_rebellion (184) =====
for s in scenarios:
    if s['id'] == 'huangjin_rebellion':
        # Han dynasty forces
        add_generals_to_faction(s, 'han_dynasty', [
            ('luzhi', 'luo_yang', 15, 95),
            ('huangfusong', 'luo_yang', 16, 92),
            ('zhujun', 'luo_yang', 14, 90),
            ('hejin', 'luo_yang', 10, 80),
            ('wangyun', 'luo_yang', 8, 85),
            ('liuyu', 'you_zhou', 10, 90),
            ('taoqian', 'xu_zhou', 8, 82),
            ('kongrong', 'xu_zhou', 6, 80),
        ])
        # Yellow Turbans
        add_generals_to_faction(s, 'yellow_turbans', [
            ('zhangbao_yt', 'ye_cheng', 10, 95),
            ('zhangliang_yt', 'ye_cheng', 10, 95),
            ('guanhai', 'xu_zhou', 6, 80),
            ('liupi', 'ru_nan', 5, 78),
            ('gongduxi', 'ru_nan', 5, 78),
            ('peixuanfeng', 'ru_nan', 4, 75),
        ])
        # Liu Bei
        add_generals_to_faction(s, 'liubei_yellow', [
            ('mizhu', 'xu_zhou', 3, 88),
            ('sunqian', 'xu_zhou', 3, 85),
            ('jianyong', 'xu_zhou', 3, 82),
            ('liaohua', 'xiang_yang', 3, 78),
        ])
        # Cao Cao
        add_generals_to_faction(s, 'caocao_yellow', [
            ('yuejin', 'chen_liu', 5, 88),
            ('hanhao', 'chen_liu', 4, 85),
            ('shihuan', 'chen_liu', 4, 82),
        ])
        # Dong Zhuo
        add_generals_to_faction(s, 'dongzhuo_yellow', [
            ('huaxiong', 'luo_yang', 8, 90),
            ('gaoshun', 'luo_yang', 7, 85),
        ])

# ===== Scenario 2: anti_dongzhuo (190) =====
    elif s['id'] == 'anti_dongzhuo':
        # Dong Zhuo
        add_generals_to_faction(s, 'dongzhuo_force', [
            ('huaxiong', 'luo_yang', 12, 92),
            ('gaoshun', 'luo_yang', 10, 88),
            ('zhangxiu', 'wan_cheng', 6, 70),
            ('caimao', 'xiang_yang', 5, 65),
        ])
        # Coalition / Liu Bei
        add_generals_to_faction(s, 'liubei_coalition', [
            ('mizhu', 'xu_zhou', 5, 90),
            ('sunqian', 'xu_zhou', 4, 88),
            ('jianyong', 'xu_zhou', 4, 85),
            ('liaohua', 'xiang_yang', 4, 80),
        ])
        # Cao Cao
        add_generals_to_faction(s, 'caocao_coalition', [
            ('yuejin', 'chen_liu', 8, 90),
            ('xunyou', 'chen_liu', 7, 88),
            ('hanhao', 'chen_liu', 6, 85),
            ('shihuan', 'chen_liu', 6, 82),
            ('caochun', 'chen_liu', 5, 88),
            ('caoang', 'chen_liu', 4, 95),
            ('manchong', 'chen_liu', 5, 82),
        ])
        # Yuan Shao
        add_generals_to_faction(s, 'yuanshao_coalition', [
            ('shenpei', 'ye_cheng', 8, 90),
            ('fengji', 'ye_cheng', 7, 85),
            ('chunyuqiong', 'ye_cheng', 8, 80),
            ('gaogan', 'jin_yang', 5, 82),
        ])
        # Sun Jian
        add_generals_to_faction(s, 'sunjian_coalition', [
            ('handang', 'chang_sha', 7, 90),
            ('lingcao', 'chang_sha', 6, 85),
            ('zhuzhi', 'chang_sha', 6, 88),
        ])
        # Liu Biao
        add_generals_to_faction(s, 'liubiao_coalition', [
            ('caimao', 'xiang_yang', 6, 82),
            ('wenpin', 'xiang_yang', 5, 85),
        ])
        # Others
        add_generals_to_faction(s, 'gongsunzan_coalition', [
            ('tianjie', 'you_zhou', 5, 80),
            ('yanrou', 'you_zhou', 4, 75),
        ])

# ===== Scenario 3: warlords_rise (194) =====
    elif s['id'] == 'warlords_rise':
        # Liu Bei
        add_generals_to_faction(s, 'liubei_rise', [
            ('mizhu', 'xiao_pei', 7, 92),
            ('sunqian', 'xiao_pei', 6, 90),
            ('jianyong', 'xiao_pei', 6, 88),
            ('chendao', 'xiao_pei', 6, 90),
            ('liaohua', 'xiao_pei', 5, 82),
        ])
        # Cao Cao
        add_generals_to_faction(s, 'caocao_rise', [
            ('xunyou', 'xu_chang', 12, 92),
            ('yuejin', 'xu_chang', 10, 90),
            ('manchong', 'xu_chang', 8, 85),
            ('caochun', 'xu_chang', 8, 90),
            ('zhongyao', 'xu_chang', 8, 88),
            ('hanhao', 'xu_chang', 8, 85),
            ('shihuan', 'xu_chang', 7, 82),
            ('maojie', 'xu_chang', 6, 85),
            ('zhuling', 'xu_chang', 6, 80),
        ])
        # Yuan Shao
        add_generals_to_faction(s, 'yuanshao_rise', [
            ('shenpei', 'ye_cheng', 12, 92),
            ('fengji', 'ye_cheng', 10, 88),
            ('chunyuqiong', 'ye_cheng', 10, 82),
            ('gaogan', 'jin_yang', 8, 85),
            ('yuantan', 'ye_cheng', 6, 80),
        ])
        # Sun Ce
        add_generals_to_faction(s, 'sunce_rise', [
            ('handang', 'wu_jun', 10, 92),
            ('jiangqin', 'wu_jun', 7, 85),
            ('zhuzhi', 'wu_jun', 8, 90),
            ('lingcao', 'wu_jun', 7, 85),
            ('dongxi', 'wu_jun', 6, 82),
            ('chenwu', 'wu_jun', 5, 80),
            ('guyong', 'wu_jun', 5, 88),
            ('zhanghong', 'jian_ye', 6, 85),
        ])
        # Lu Bu
        add_generals_to_faction(s, 'lvbu_rise', [
            ('gaoshun', 'xia_pi', 12, 95),
            ('lvlingqi', 'xia_pi', 5, 90),
        ])
        # Yuan Shu
        add_generals_to_faction(s, 'yuanshu_rise', [
            ('yanxiang', 'shou_chun', 6, 82),
        ])
        # Liu Zhang
        add_generals_to_faction(s, 'liuzhang_rise', [
            ('yanyan', 'jiang_zhou', 10, 88),
            ('huangquan', 'cheng_du', 7, 85),
            ('zhangsong', 'cheng_du', 5, 60),
            ('qinmi', 'cheng_du', 4, 80),
            ('liyan', 'cheng_du', 5, 75),
        ])
        # Liu Biao
        add_generals_to_faction(s, 'liubiao_rise', [
            ('wenpin', 'xiang_yang', 8, 90),
            ('caimao', 'xiang_yang', 8, 85),
            ('zhangyun', 'xiang_yang', 5, 80),
        ])
        # Ma Teng
        add_generals_to_faction(s, 'mateng_rise', [
            ('pang_de', 'tian_shui', 8, 90),
            ('madai', 'tian_shui', 5, 85),
            ('hansui', 'wu_wei', 10, 60),
            ('chenggongying', 'wu_wei', 6, 80),
            ('maxiu', 'tian_shui', 4, 88),
            ('matie', 'tian_shui', 3, 85),
        ])
        # Zhang Lu
        add_generals_to_faction(s, 'zhanglu_rise', [
            ('yangsong', 'han_zhong', 5, 55),
            ('yangbai', 'han_zhong', 5, 80),
        ])

# ===== Scenario 4: guandu_battle (200) =====
    elif s['id'] == 'guandu_battle':
        # Cao Cao
        add_generals_to_faction(s, 'caocao_guandu', [
            ('xunyou', 'xu_chang', 15, 95),
            ('yuejin', 'xu_chang', 14, 92),
            ('manchong', 'xu_chang', 12, 88),
            ('caochun', 'xu_chang', 12, 92),
            ('caoxiu', 'xu_chang', 6, 82),
            ('zhongyao', 'xu_chang', 12, 90),
            ('maojie', 'xu_chang', 8, 85),
            ('cuiyan', 'ye_cheng', 7, 82),
            ('hanhao', 'xu_chang', 10, 88),
            ('shihuan', 'xu_chang', 10, 85),
            ('zhuling', 'xu_chang', 9, 82),
            ('caimao', 'xiang_yang', 8, 78),
            ('wenpin', 'xiang_yang', 10, 88),
        ])
        # Yuan Shao
        add_generals_to_faction(s, 'yuanshao_guandu', [
            ('shenpei', 'ye_cheng', 14, 95),
            ('fengji', 'ye_cheng', 12, 88),
            ('chunyuqiong', 'ye_cheng', 12, 82),
            ('gaogan', 'jin_yang', 10, 85),
            ('yuantan', 'ye_cheng', 8, 78),
            ('jugui', 'ye_cheng', 5, 82),
            ('zanghong', 'ye_cheng', 8, 88),
        ])
        # Liu Bei
        add_generals_to_faction(s, 'liubei_guandu', [
            ('mizhu', 'xu_zhou', 10, 95),
            ('sunqian', 'xu_zhou', 8, 92),
            ('jianyong', 'xu_zhou', 8, 90),
            ('chendao', 'xu_zhou', 10, 92),
            ('liaohua', 'xu_zhou', 8, 85),
            ('liufeng', 'xu_zhou', 5, 80),
        ])
        # Sun Quan
        add_generals_to_faction(s, 'sunquan_guandu', [
            ('handang', 'jian_ye', 12, 92),
            ('jiangqin', 'jian_ye', 10, 88),
            ('zhuzhi', 'wu_jun', 10, 90),
            ('lingcao', 'wu_jun', 9, 85),
            ('dongxi', 'wu_jun', 8, 82),
            ('chenwu', 'wu_jun', 7, 80),
            ('panzhang', 'chai_sang', 5, 78),
            ('xusheng', 'wu_jun', 5, 80),
            ('guyong', 'jian_ye', 8, 90),
            ('kanze', 'kuai_ji', 5, 85),
            ('zhanghong', 'jian_ye', 8, 88),
            ('buzhi', 'jian_ye', 5, 82),
            ('sunshangxiang', 'jian_ye', 4, 85),
        ])
        # Liu Biao
        add_generals_to_faction(s, 'liubiao_guandu', [
            ('wenpin', 'xiang_yang', 12, 92),
            ('zhangyun', 'xiang_yang', 7, 82),
            ('liuqi', 'jiang_xia', 4, 80),
        ])
        # Liu Zhang
        add_generals_to_faction(s, 'liuzhang_guandu', [
            ('yanyan', 'jiang_zhou', 12, 90),
            ('huangquan', 'cheng_du', 10, 88),
            ('zhangsong', 'cheng_du', 7, 55),
            ('liyan', 'cheng_du', 8, 78),
            ('wuyi', 'cheng_du', 6, 82),
            ('qinmi', 'cheng_du', 6, 80),
        ])
        # Ma Teng
        add_generals_to_faction(s, 'mateng_guandu', [
            ('pang_de', 'tian_shui', 12, 92),
            ('madai', 'tian_shui', 8, 85),
            ('hansui', 'wu_wei', 12, 58),
            ('chenggongying', 'wu_wei', 8, 82),
            ('maxiu', 'tian_shui', 6, 88),
            ('matie', 'tian_shui', 5, 85),
        ])
        # Zhang Lu
        add_generals_to_faction(s, 'zhanglu_guandu', [
            ('yangsong', 'han_zhong', 6, 52),
            ('yangbai', 'han_zhong', 7, 82),
        ])

# ===== Scenario 5: three_visits (207) =====
    elif s['id'] == 'three_visits':
        # Liu Bei
        add_generals_to_faction(s, 'liubei_visits', [
            ('mizhu', 'xin_ye', 12, 95),
            ('sunqian', 'xin_ye', 10, 92),
            ('jianyong', 'xin_ye', 10, 90),
            ('chendao', 'xin_ye', 12, 92),
            ('liaohua', 'xin_ye', 10, 85),
            ('liufeng', 'xin_ye', 8, 82),
            ('mifang', 'xin_ye', 6, 60),
        ])
        # Cao Cao
        add_generals_to_faction(s, 'caocao_visits', [
            ('xunyou', 'xu_chang', 18, 95),
            ('yuejin', 'xu_chang', 16, 92),
            ('manchong', 'xu_chang', 14, 90),
            ('caochun', 'xu_chang', 14, 92),
            ('caozhen', 'luo_yang', 8, 85),
            ('caoxiu', 'xu_chang', 10, 85),
            ('zhongyao', 'xu_chang', 14, 92),
            ('chenqun', 'xu_chang', 8, 85),
            ('yangxiu', 'xu_chang', 6, 72),
            ('cuiyan', 'ye_cheng', 10, 85),
            ('sinpi', 'xu_chang', 8, 82),
            ('wenpin', 'xiang_yang', 14, 90),
            ('zangba', 'xu_zhou', 12, 85),
            ('maojie', 'xu_chang', 10, 88),
            ('hanhao', 'xu_chang', 12, 88),
            ('shihuan', 'xu_chang', 12, 85),
            ('zhuling', 'xu_chang', 12, 82),
            ('lvqian', 'xu_zhou', 8, 82),
            ('zhangxiu', 'wan_cheng', 12, 75),
            ('caimao', 'xiang_yang', 10, 78),
            ('zhangyun', 'xiang_yang', 8, 80),
        ])
        # Sun Quan
        add_generals_to_faction(s, 'sunquan_visits', [
            ('handang', 'jian_ye', 14, 92),
            ('jiangqin', 'jian_ye', 12, 88),
            ('lingtong', 'jian_ye', 8, 82),
            ('zhuzhi', 'wu_jun', 12, 90),
            ('dongxi', 'wu_jun', 10, 85),
            ('chenwu', 'jian_ye', 9, 82),
            ('panzhang', 'chai_sang', 8, 78),
            ('xusheng', 'wu_jun', 8, 82),
            ('zhugejin', 'jian_ye', 8, 88),
            ('guyong', 'jian_ye', 10, 92),
            ('kanze', 'jian_ye', 8, 88),
            ('buzhi', 'jian_ye', 7, 85),
            ('yufan', 'kuai_ji', 6, 78),
            ('zhanghong', 'jian_ye', 10, 90),
            ('sunshangxiang', 'jian_ye', 6, 85),
            ('dingfeng', 'lu_jiang', 5, 82),
            ('quancong', 'jian_ye', 4, 78),
            ('lvfan', 'jian_ye', 8, 85),
            ('daqiao', 'wu_jun', 5, 80),
            ('xiaoqiao', 'wu_jun', 5, 82),
        ])
        # Liu Biao
        add_generals_to_faction(s, 'liubiao_visits', [
            ('liuqi', 'jiang_xia', 6, 82),
            ('liucong', 'xiang_yang', 3, 78),
        ])
        # Liu Zhang
        add_generals_to_faction(s, 'liuzhang_visits', [
            ('yanyan', 'jiang_zhou', 14, 90),
            ('huangquan', 'cheng_du', 12, 88),
            ('zhangsong', 'cheng_du', 10, 50),
            ('liyan', 'cheng_du', 10, 75),
            ('wuyi', 'cheng_du', 8, 82),
            ('wuban', 'cheng_du', 5, 80),
            ('qinmi', 'cheng_du', 8, 82),
            ('liuba', 'zi_tong', 6, 70),
        ])
        # Ma Teng
        add_generals_to_faction(s, 'machao_visits', [
            ('pang_de', 'tian_shui', 14, 92),
            ('madai', 'tian_shui', 10, 88),
            ('maxiu', 'tian_shui', 7, 88),
            ('matie', 'tian_shui', 6, 85),
            ('hansui', 'wu_wei', 14, 55),
            ('chenggongying', 'wu_wei', 10, 82),
        ])
        # Zhang Lu
        add_generals_to_faction(s, 'zhanglu_visits', [
            ('yangsong', 'han_zhong', 8, 48),
            ('yangbai', 'han_zhong', 8, 82),
        ])
        # Meng Huo
        add_generals_to_faction(s, 'menghuo_visits', [
            ('mengyou', 'wu_ling', 6, 90),
            ('zhurong', 'wu_ling', 8, 92),
            ('wutugu', 'ling_ling', 6, 85),
            ('shamoke', 'wu_ling', 5, 80),
        ])

# ===== Scenario 6: red_cliffs (208) =====
    elif s['id'] == 'red_cliffs':
        # Cao Cao
        add_generals_to_faction(s, 'caocao_redcliffs', [
            ('xunyou', 'xiang_yang', 18, 95),
            ('yuejin', 'xiang_yang', 16, 92),
            ('manchong', 'xu_chang', 15, 90),
            ('caochun', 'xu_chang', 15, 92),
            ('caozhen', 'luo_yang', 10, 88),
            ('caoxiu', 'xu_chang', 12, 85),
            ('zhongyao', 'xu_chang', 15, 92),
            ('chenqun', 'xu_chang', 10, 88),
            ('wenpin', 'jiang_ling', 15, 90),
            ('zangba', 'xu_chang', 13, 85),
            ('caimao', 'xiang_yang', 12, 78),
            ('zhangyun', 'xiang_yang', 10, 80),
            ('maojie', 'xu_chang', 12, 88),
            ('zhangxiu', 'wan_cheng', 13, 78),
            ('hanhao', 'xu_chang', 13, 88),
            ('shihuan', 'xu_chang', 13, 85),
            ('zhuling', 'xu_chang', 13, 82),
            ('lvqian', 'xu_chang', 10, 82),
            ('niujin', 'jiang_ling', 6, 80),
            ('yangxiu', 'xu_chang', 8, 70),
        ])
        # Liu Bei
        add_generals_to_faction(s, 'liubei_alliance', [
            ('mizhu', 'xia_pi', 14, 95),
            ('sunqian', 'xia_pi', 12, 92),
            ('jianyong', 'xia_pi', 12, 90),
            ('chendao', 'xia_pi', 14, 95),
            ('liaohua', 'jiang_xia', 12, 85),
            ('liufeng', 'xia_pi', 10, 82),
            ('mifang', 'jiang_xia', 8, 58),
            ('mengda', 'jiang_xia', 7, 65),
            ('dengzhi', 'jiang_xia', 6, 82),
        ])
        # Sun Quan
        add_generals_to_faction(s, 'sun_liu_alliance', [
            ('handang', 'jian_ye', 14, 92),
            ('jiangqin', 'chai_sang', 13, 88),
            ('lingtong', 'jian_ye', 10, 82),
            ('zhuzhi', 'wu_jun', 12, 90),
            ('dongxi', 'wu_jun', 11, 85),
            ('chenwu', 'lu_jiang', 10, 82),
            ('panzhang', 'chai_sang', 9, 78),
            ('xusheng', 'wu_jun', 9, 82),
            ('zhugejin', 'jian_ye', 10, 90),
            ('guyong', 'jian_ye', 11, 92),
            ('kanze', 'jian_ye', 9, 88),
            ('buzhi', 'jian_ye', 8, 85),
            ('yufan', 'kuai_ji', 8, 78),
            ('zhanghong', 'jian_ye', 11, 90),
            ('sunshangxiang', 'jian_ye', 8, 85),
            ('dingfeng', 'lu_jiang', 6, 82),
            ('quancong', 'jian_ye', 5, 78),
            ('lvfan', 'jian_ye', 9, 85),
            ('daqiao', 'wu_jun', 6, 82),
            ('xiaoqiao', 'wu_jun', 6, 85),
            ('zhuran', 'jian_ye', 5, 80),
            ('zhuhuan', 'wu_jun', 5, 80),
        ])
        # Liu Zhang
        add_generals_to_faction(s, 'liuzhang_redcliffs', [
            ('yanyan', 'jiang_zhou', 14, 90),
            ('huangquan', 'cheng_du', 12, 88),
            ('zhangsong', 'cheng_du', 10, 48),
            ('liyan', 'cheng_du', 12, 75),
            ('wuyi', 'cheng_du', 10, 82),
            ('wuban', 'cheng_du', 6, 80),
            ('qinmi', 'cheng_du', 9, 82),
            ('liuba', 'cheng_du', 8, 68),
        ])
        # Ma Teng
        add_generals_to_faction(s, 'machao_redcliffs', [
            ('pang_de', 'tian_shui', 14, 92),
            ('madai', 'tian_shui', 10, 88),
            ('maxiu', 'tian_shui', 8, 88),
            ('matie', 'tian_shui', 7, 85),
            ('hansui', 'wu_wei', 14, 52),
            ('chenggongying', 'wu_wei', 10, 82),
        ])
        # Zhang Lu
        add_generals_to_faction(s, 'zhanglu_redcliffs', [
            ('yangsong', 'han_zhong', 8, 45),
            ('yangbai', 'han_zhong', 8, 82),
        ])
        # Meng Huo
        add_generals_to_faction(s, 'menghuo_redcliffs', [
            ('mengyou', 'wu_ling', 7, 90),
            ('zhurong', 'wu_ling', 9, 92),
            ('wutugu', 'ling_ling', 7, 85),
            ('shamoke', 'wu_ling', 6, 80),
        ])

# Write updated scenarios
with open('CatSanguo/Data/scenarios.json', 'w', encoding='utf-8') as f:
    json.dump(scenarios, f, ensure_ascii=False, indent=2)

# Count totals
total_added = 0
for s in scenarios:
    scenario_count = sum(len(f['initialGenerals']) for f in s['factions'])
    print(f"Scenario {s['id']}: {scenario_count} generals across {len(s['factions'])} factions")
    total_added += scenario_count

print(f"\nTotal general assignments across all scenarios: {total_added}")
print("scenarios.json written successfully")
