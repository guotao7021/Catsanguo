import json

f = open(r'd:\CatSanguo\CatSanguo\Data\generals_new_batch.json', 'r', encoding='utf-8-sig')
data = json.load(f)
f.close()

valid_cities = {'cheng_du','zi_tong','mian_zhu','jian_ge','han_zhong','bai_di','jiang_zhou','yi_ling','chang_an','tong_guan','tian_shui','wu_wei','luo_yang','hu_lao','xu_chang','ye_cheng','chen_liu','wan_cheng','pu_yang','ru_nan','nan_yang','jin_yang','hu_guan','you_zhou','bei_ping','ping_yuan','xu_zhou','xia_pi','xiao_pei','shou_chun','he_fei','jian_ye','chai_sang','wu_jun','kuai_ji','lu_jiang','shan_yin','xiang_yang','jiang_ling','jiang_xia','chang_sha','ling_ling','wu_ling','gui_yang'}
invalid = [(g['id'], g['appearCityId']) for g in data if g['appearCityId'] not in valid_cities]
print("Invalid cities:", invalid)

wu_count = sum(1 for g in data if g['activeSkillId'].startswith('wu_'))
qun_count = sum(1 for g in data if g['activeSkillId'].startswith('qun_'))
print("Wu generals:", wu_count)
print("Others generals:", qun_count)

for g in data:
    for stat in ['strength','intelligence','command','politics','speed','loyalty','charisma']:
        v = g[stat]
        if v < 1 or v > 100:
            print(g['id'], "has", stat, "=", v, "out of range")
    if g['salary'] < 15 or g['salary'] > 50:
        print(g['id'], "has salary =", g['salary'], "out of range")
    if g['preferredFormation'] not in ['cavalry','vanguard','archer']:
        print(g['id'], "has invalid formation:", g['preferredFormation'])

# Check no overlap with existing generals
existing_ids = {'sunquan','zhouyu','zhaoyun','ganning','taishici','luxun','lusu','lvmeng','zhoutai','chengpu','huanggai','zhangzhao','sunce','sunjian','diaochan','lvbu','dongzhuo','yuanshao','yuan_shu','gongsunzan','liubiao','zhangren','menghuo','zhangjiao','li_ru','chen_gong','ji_ling','ma_teng','liuyan','liuzhang','zhanglu','yan_liang','wen_chou','gao_lan','ju_shou','tian_feng','dengai','zhonghui'}
overlap = [g['id'] for g in data if g['id'] in existing_ids]
print("Overlapping IDs with existing:", overlap)

print("Validation complete")
