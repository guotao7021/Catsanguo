#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
add_remaining_generals.py
为三国志11风格游戏补充缺失武将，使总数达到约719人。
读取已有武将数据，生成约419个新武将并追加到generals.json。
"""

import json
import os

DATA_DIR = os.path.dirname(os.path.abspath(__file__))
GENERALS_FILE = os.path.join(DATA_DIR, "generals.json")
CURRENT_IDS_FILE = os.path.join(DATA_DIR, "current_ids.txt")

# ============================================================
# 1. 读取已有武将ID
# ============================================================
def load_existing_ids():
    with open(CURRENT_IDS_FILE, "r", encoding="utf-8") as f:
        content = f.read().strip()
    # 格式: id1/nid2/nid3/nid4...
    parts = content.split("/")
    ids = set()
    for p in parts:
        p = p.strip()
        if p.startswith("n"):
            ids.add(p[1:])
        elif p:
            ids.add(p)
    return ids


def load_existing_generals():
    with open(GENERALS_FILE, "r", encoding="utf-8") as f:
        return json.load(f)


# ============================================================
# 2. 新武将数据定义
#    格式: (id, 姓名, 称号, 武, 智, 统, 政, 速, 忠, 初登场, 出现城市,
#           特殊技能列表, 俸禄, 主动技能, 被动技能, 兵种, 魅力)
# ============================================================

def generate_new_generals(existing_ids):
    """生成所有新武将数据"""
    new_generals = []
    
    # ---- 城市列表 ----
    cities = [
        "luo_yang", "chang_an", "xu_zhou", "ye_cheng", "cheng_du",
        "jian_ye", "shou_chun", "xiang_yang", "jiang_ling", "chang_sha",
        "wu_ling", "ling_ling", "gui_yang", "zi_tong", "ba_xi",
        "han_zhong", "tian_shui", "an_ding", "wu_wei", "jin_cheng",
        "bei_ping", "nan_pi", "ping_yuan", "qing_he", "bo_hai",
        "chen_liu", "ru_nan", "xi_pi", "wan_cheng", "xin_ye",
        "shang_yong", "yong_an", "jian_an", "yu_zhang", "lu_jiang",
        "mo_ling", "wu_jun", "kuai_ji", "fu_jian", "jiao_zhi",
        "yun_nan", "jian_wei", "zhu_ti", "mi_yun", "you_zhou",
        "dai_jun", "shang_dang", "tai_yuan", "he_dong", "hong_nong",
    ]

    # ---- 辅助函数 ----
    def make_g(id_, name, title, str_, intel, cmd, pol, spd, loy, year, city,
               skills, salary, active, passive, formation, charisma):
        if id_ in existing_ids:
            return None
        existing_ids.add(id_)
        return {
            "id": id_,
            "name": name,
            "title": title,
            "strength": str_,
            "intelligence": intel,
            "command": cmd,
            "politics": pol,
            "speed": spd,
            "loyalty": loy,
            "appearYear": year,
            "appearCityId": city,
            "specialSkills": skills,
            "salary": salary,
            "activeSkillId": active,
            "passiveSkillId": passive,
            "preferredFormation": formation,
            "charisma": charisma
        }

    def add(id_, name, title, str_, intel, cmd, pol, spd, loy, year, city,
            skills=None, salary=20, active="", passive="", formation="infantry", charisma=50):
        if skills is None:
            skills = []
        g = make_g(id_, name, title, str_, intel, cmd, pol, spd, loy, year, city,
                   skills, salary, active, passive, formation, charisma)
        if g:
            new_generals.append(g)
            return True
        return False

    # ================================================================
    # 第一类：曹魏文臣 / 武将（中后期）
    # ================================================================
    add("chenlin", "陈琳", "建安七子", 25, 72, 45, 68, 42, 75, 184, "ye_cheng",
        ["literary_talent"], 22, "qun_literary_fame", "qun_seven_scholars", "archer", 65)
    add("yingyang", "应玚", "建安七子", 22, 65, 38, 62, 40, 72, 184, "ye_cheng",
        [], 18, "qun_literary_fame", "qun_seven_scholars", "archer", 58)
    add("liugan", "刘干", "建安七子", 30, 62, 42, 58, 45, 70, 184, "ye_cheng",
        [], 18, "qun_literary_fame", "qun_seven_scholars", "archer", 55)
    add("ruanyu", "阮瑀", "建安七子", 28, 70, 40, 65, 42, 72, 184, "chen_liu",
        [], 20, "qun_literary_fame", "qun_seven_scholars", "archer", 60)
    add("wangcan", "王粲", "建安七子", 20, 78, 35, 72, 38, 70, 184, "chang_an",
        ["memory_master"], 22, "qun_literary_fame", "qun_photo_memory", "archer", 62)
    add("kongrong2", "徐干", "建安七子", 24, 74, 38, 70, 40, 74, 184, "bei_ping",
        [], 20, "qun_literary_fame", "qun_seven_scholars", "archer", 60)

    add("manchong2", "满宠", "魏国名臣", 42, 68, 75, 72, 48, 88, 184, "shan_yang",
        ["strict_law"], 26, "qun_crisis_negotiation", "qun_strict_enforcer", "archer", 58)
    add("simaf", "司马孚", "魏室忠臣", 48, 72, 68, 78, 45, 90, 208, "luo_yang",
        [], 26, "qun_crisis_negotiation", "qun_loyal_kinsman", "archer", 68)
    add("chenqun", "陈群", "九品中正", 30, 82, 62, 88, 42, 85, 184, "ping_yuan",
        ["nine_rank_system"], 32, "qun_admin_reform", "qun_nine_rank", "archer", 78)
    add("huanjie", "桓阶", "魏国重臣", 32, 75, 55, 80, 42, 88, 194, "chang_sha",
        [], 24, "qun_admin_reform", "qun_trusted_minister", "archer", 70)
    add("gaotanglong", "高堂隆", "谏臣", 28, 78, 45, 82, 40, 90, 200, "tai_shan",
        ["frank_remonstrance"], 24, "qun_honest_remonstrance", "qun_upright_scholar", "archer", 72)
    add("duji", "杜畿", "魏国良吏", 38, 70, 72, 82, 45, 85, 184, "he_dong",
        ["local_governance"], 26, "qun_admin_reform", "qun_capable_governor", "archer", 70)
    add("zhenghun", "郑浑", "魏国良吏", 30, 65, 55, 78, 42, 82, 184, "kai_feng",
        [], 22, "qun_admin_reform", "qun_capable_governor", "archer", 65)
    add("cuiyan", "崔琰", "河北名士", 35, 75, 48, 78, 42, 82, 184, "bo_hai",
        ["righteous_character"], 26, "qun_righteous_oath", "qun_noble_scholar", "archer", 75)
    add("wangxiu", "王修", "忠义之士", 45, 65, 58, 72, 48, 88, 184, "bei_hai",
        [], 22, "qun_crisis_negotiation", "qun_loyal_scholar", "archer", 65)
    add("zhongyao", "钟繇", "书法大家", 32, 78, 55, 85, 40, 88, 184, "chang_an",
        ["calligraphy_master"], 30, "qun_literary_fame", "qun_calligraphy_sage", "archer", 80)
    add("huanfan", "华歆", "魏国三公", 25, 75, 45, 82, 38, 80, 184, "ping_yuan",
        [], 26, "qun_admin_reform", "qun_three_excellencies", "archer", 72)
    add("xinsunji", "辛毗", "刚正谏臣", 30, 72, 52, 78, 42, 85, 184, "ye_cheng",
        [], 24, "qun_honest_remonstrance", "qun_upright_scholar", "archer", 68)
    add("yangfu", "杨阜", "凉州名士", 48, 72, 62, 68, 50, 85, 194, "tian_shui",
        ["loyal_remonstrance"], 24, "qun_honest_remonstrance", "qun_loyal_scholar", "archer", 65)
    add("jia_kui", "贾逵", "魏国名臣", 52, 75, 72, 70, 52, 85, 184, "he_dong",
        [], 26, "qun_crisis_negotiation", "qun_capable_minister", "archer", 62)
    add("guanhuan", "管宁", "隐士", 15, 82, 25, 65, 35, 90, 184, "bei_ping",
        ["recluse_virtue"], 18, "qun_literary_fame", "qun_recluse_sage", "archer", 85)
    add("wanglang", "王朗", "魏国三公", 22, 78, 42, 82, 38, 78, 184, "tan_cheng",
        [], 26, "qun_admin_reform", "qun_three_excellencies", "archer", 72)
    add("sun_li", "孙礼", "魏国将领", 58, 62, 68, 60, 55, 82, 200, "zhuo_xian",
        [], 22, "qun_steady_strike", "qun_reliable_general", "vanguard", 52)
    add("gao_rou", "高柔", "魏国法臣", 35, 75, 58, 82, 42, 88, 194, "chen_liu",
        ["legal_expert"], 26, "qun_strict_enforcer", "qun_legal_scholar", "archer", 70)
    add("wei_zhen", "卫臻", "魏国重臣", 32, 72, 55, 78, 40, 82, 200, "chen_liu",
        [], 24, "qun_admin_reform", "qun_trusted_minister", "archer", 68)
    add("lu_yu", "卢毓", "魏国名臣", 28, 74, 48, 80, 42, 85, 200, "zhuo_xian",
        [], 24, "qun_admin_reform", "qun_capable_minister", "archer", 70)

    # 曹魏武将
    add("zhu_ling", "朱灵", "曹魏老将", 78, 55, 72, 52, 65, 75, 184, "qing_he",
        [], 24, "qun_steady_strike", "qun_veteran_general", "vanguard", 48)
    add("liu_tai", "刘泰", "曹魏将领", 62, 48, 58, 45, 60, 72, 194, "ye_cheng",
        [], 18, "qun_steady_strike", "qun_reliable_general", "vanguard", 42)
    add("niu_jin", "牛金", "曹魏猛将", 82, 38, 62, 32, 72, 70, 194, "luo_yang",
        [], 22, "qun_fierce_strike", "qun_brave_warrior", "vanguard", 35)
    add("wang_zhang", "王昶", "魏国都督", 55, 68, 72, 65, 52, 80, 200, "ru_nan",
        [], 24, "qun_steady_strike", "qun_regional_commander", "archer", 58)
    add("guan_qiu_jian", "毌丘俭", "魏国叛将", 68, 72, 78, 62, 55, 60, 228, "shou_chun",
        ["rebellion"], 28, "qun_rebellion", "qun_ambitious_general", "vanguard", 52)
    add("zhuge_dan", "诸葛诞", "淮南三叛", 72, 70, 80, 65, 58, 55, 228, "shou_chun",
        [], 30, "qun_rebellion", "qun_ambitious_general", "vanguard", 55)
    add("wen_qin", "文钦", "淮南叛将", 75, 52, 68, 42, 68, 58, 228, "shou_chun",
        [], 24, "qun_rebellion", "qun_rebel_general", "vanguard", 45)
    add("wen_yang", "文鸯", "小赵云", 88, 55, 72, 42, 85, 70, 250, "shou_chun",
        ["brave_charge"], 30, "qun_fierce_strike", "qun_young_hero", "cavalry", 48)
    add("tang_zi", "唐咨", "魏国降将", 55, 58, 52, 48, 52, 50, 228, "shou_chun",
        [], 18, "qun_surrender_offer", "qun_turncoat", "vanguard", 38)

    # 曹魏宗室
    add("caozhen", "曹真", "魏国大将军", 72, 72, 85, 68, 58, 92, 184, "chen_liu",
        ["steady_command"], 32, "qun_steady_strike", "qun_grand_commander", "vanguard", 62)
    add("caoxiu", "曹休", "魏国大司马", 75, 62, 80, 58, 72, 88, 184, "chen_liu",
        [], 30, "qun_steady_strike", "qun_grand_marshal", "cavalry", 55)
    add("caoxiong", "曹熊", "曹操之子", 28, 45, 32, 38, 35, 80, 184, "ye_cheng",
        [], 15, "", "qun_sickly_prince", "archer", 42)
    add("caoju", "曹据", "曹操之子", 32, 42, 35, 40, 38, 80, 184, "ye_cheng",
        [], 15, "", "qun_minor_prince", "archer", 40)
    add("caoyu", "曹宇", "曹操之子", 35, 48, 38, 42, 40, 82, 184, "ye_cheng",
        [], 16, "", "qun_minor_prince", "archer", 42)
    add("caolin", "曹林", "曹操之子", 35, 45, 38, 42, 40, 82, 184, "ye_cheng",
        [], 16, "", "qun_minor_prince", "archer", 40)
    add("caogong", "曹干", "曹操之子", 30, 40, 32, 35, 35, 80, 200, "ye_cheng",
        [], 14, "", "qun_minor_prince", "archer", 38)
    add("caobiao", "曹彪", "曹操之子", 38, 42, 35, 38, 40, 78, 184, "ye_cheng",
        [], 15, "", "qun_minor_prince", "archer", 40)
    add("cao_rui", "曹叡", "魏明帝", 58, 78, 75, 72, 52, 85, 200, "ye_cheng",
        ["imperial_vision"], 35, "qun_imperial_mandate", "qun_enlightened_emperor", "archer", 72)
    add("cao_fang", "曹芳", "魏少帝", 32, 55, 42, 48, 40, 70, 228, "luo_yang",
        [], 22, "", "qun_puppet_emperor", "archer", 55)
    add("cao_mao", "曹髦", "魏高贵乡公", 52, 68, 55, 52, 55, 65, 240, "luo_yang",
        ["courageous_spirit"], 24, "qun_courageous_charge", "qun_tragic_emperor", "archer", 62)
    add("cao_huan", "曹奂", "魏元帝", 30, 52, 38, 45, 38, 60, 250, "luo_yang",
        [], 20, "", "qun_last_emperor", "archer", 52)
    add("caoshuang", "曹爽", "魏国权臣", 48, 62, 58, 52, 48, 65, 228, "luo_yang",
        ["arrogant_rule"], 26, "qun_arrogant_command", "qun_foolish_regent", "archer", 42)
    add("caoxun", "曹训", "曹爽之弟", 45, 42, 42, 35, 50, 70, 228, "luo_yang",
        [], 16, "", "qun_minor_noble", "vanguard", 35)
    add("caoyan", "曹彦", "曹爽之弟", 42, 40, 38, 32, 48, 70, 228, "luo_yang",
        [], 16, "", "qun_minor_noble", "vanguard", 32)
    add("heyan", "何晏", "玄学家", 22, 72, 32, 62, 40, 55, 200, "nan_yang",
        ["metaphysics"], 22, "qun_literary_fame", "qun_metaphysical_scholar", "archer", 58)
    add("dengyang", "邓飏", "曹爽党羽", 32, 58, 42, 52, 42, 58, 200, "nan_yang",
        [], 18, "", "qun_faction_member", "archer", 45)
    add("dingmi", "丁谧", "曹爽党羽", 28, 62, 38, 55, 40, 60, 200, "peixian",
        [], 18, "", "qun_faction_member", "archer", 48)
    add("bi_gui", "毕轨", "曹爽党羽", 35, 58, 45, 55, 42, 60, 200, "dong_ping",
        [], 18, "", "qun_faction_member", "archer", 45)
    add("lisheng", "李胜", "曹爽党羽", 30, 60, 40, 55, 42, 58, 200, "nan_yang",
        [], 18, "", "qun_faction_member", "archer", 48)

    # ================================================================
    # 第二类：蜀汉文臣 / 武将
    # ================================================================
    add("qiaozhou2", "谯周", "蜀中大学者", 18, 85, 35, 72, 35, 75, 200, "ba_xi",
        ["scholarly_wisdom"], 26, "qun_literary_fame", "qun_great_scholar", "archer", 72)
    add("xi_zheng", "郤正", "蜀汉文臣", 22, 75, 32, 68, 38, 78, 220, "luo_yang",
        [], 22, "qun_literary_fame", "qun_literary_scholar", "archer", 62)
    add("chen_zhi", "陈祗", "蜀汉尚书", 32, 72, 48, 75, 42, 80, 220, "ru_nan",
        [], 24, "qun_admin_reform", "qun_capable_minister", "archer", 65)
    add("zhuge_zhan", "诸葛瞻", "诸葛之子", 62, 75, 68, 62, 55, 92, 228, "cheng_du",
        ["father_legacy"], 28, "qun_steady_strike", "qun_son_of_dragon", "archer", 68)
    add("zhuge_shang", "诸葛尚", "诸葛之孙", 58, 62, 55, 48, 60, 90, 250, "cheng_du",
        [], 22, "qun_courageous_charge", "qun_grandson_hero", "vanguard", 55)
    add("zhang_yi", "张翼", "蜀汉老将", 72, 58, 75, 52, 62, 85, 194, "jian_wei",
        [], 24, "qun_steady_strike", "qun_veteran_general", "vanguard", 52)
    add("zong_yu", "宗预", "蜀汉使臣", 35, 68, 45, 65, 42, 80, 207, "nan_yang",
        [], 20, "qun_diplomatic_counsel", "qun_eloquent_envoy", "archer", 58)
    add("lai_min", "来敏", "蜀汉学者", 22, 78, 30, 62, 38, 72, 194, "yi_yang",
        [], 22, "qun_literary_fame", "qun_scholar", "archer", 60)
    add("yin_mo", "尹默", "蜀汉学者", 18, 80, 25, 58, 35, 70, 194, "zi_tong",
        [], 20, "qun_literary_fame", "qun_scholar", "archer", 55)
    add("li_zhao", "李譔", "蜀汉学者", 25, 75, 28, 55, 38, 72, 200, "zi_tong",
        [], 18, "qun_literary_fame", "qun_scholar", "archer", 52)
    add("du_qiong", "杜琼", "蜀汉学者", 22, 72, 30, 58, 35, 75, 194, "cheng_du",
        [], 20, "qun_literary_fame", "qun_scholar", "archer", 58)
    add("meng_guang", "孟光", "蜀汉老臣", 32, 65, 38, 62, 42, 78, 194, "luo_xian",
        [], 20, "qun_honest_remonstrance", "qun_veteran_minister", "archer", 60)
    add("huang_hao", "黄皓", "宦官乱政", 15, 58, 22, 48, 35, 40, 228, "cheng_du",
        ["court_intrigue"], 18, "qun_court_power", "qun_corrupt_eunuch", "archer", 22)
    add("chen_shou", "陈寿", "三国志作者", 18, 82, 28, 65, 35, 80, 250, "an_han",
        ["historian"], 24, "qun_literary_fame", "qun_historian_sage", "archer", 68)
    add("luo_xian", "罗宪", "蜀汉将领", 58, 62, 62, 55, 52, 78, 250, "yong_an",
        [], 22, "qun_steady_strike", "qun_reliable_general", "vanguard", 52)
    add("huo_yi", "霍弋", "蜀汉南中都督", 55, 65, 72, 58, 52, 80, 220, "yun_nan",
        [], 24, "qun_steady_strike", "qun_southern_commander", "vanguard", 55)
    add("xiang_chong2", "向宠", "蜀汉将领", 62, 58, 65, 48, 55, 85, 207, "yi_yang",
        [], 22, "qun_steady_strike", "qun_reliable_general", "vanguard", 50)
    add("ma_zhong_shu", "马忠(蜀)", "蜀汉镇南大将军", 68, 62, 72, 55, 62, 82, 207, "ba_xi",
        [], 24, "qun_steady_strike", "qun_southern_pacifier", "vanguard", 52)
    add("zhang_neng", "张嶷", "蜀汉名将", 75, 58, 72, 48, 65, 85, 200, "ba_xi",
        [], 26, "qun_steady_strike", "qun_brave_general", "vanguard", 55)
    add("gou_fu", "句扶", "蜀汉将领", 62, 52, 60, 42, 58, 78, 207, "ba_xi",
        [], 20, "qun_steady_strike", "qun_reliable_general", "vanguard", 48)

    # ================================================================
    # 第三类：东吴文臣 / 武将
    # ================================================================
    add("gu_yong", "顾雍", "东吴丞相", 28, 80, 62, 88, 38, 90, 194, "wu_jun",
        ["steady_governance"], 32, "qun_admin_reform", "qun_prime_minister", "archer", 82)
    add("zhang_hong_dongwu", "张纮", "东吴谋臣", 30, 78, 55, 72, 40, 82, 194, "guang_ling",
        [], 24, "qun_strategic_plan", "qun_wise_counselor", "archer", 70)
    add("zhao_zi", "赵咨", "东吴使臣", 28, 72, 42, 65, 42, 80, 200, "nan_yang",
        [], 20, "qun_diplomatic_counsel", "qun_eloquent_envoy", "archer", 58)
    add("shen_you", "沈友", "东吴少年才", 42, 72, 48, 58, 55, 70, 194, "wu_jun",
        [], 20, "qun_literary_fame", "qun_young_genius", "archer", 55)
    add("shi_xie", "士燮", "交州太守", 32, 72, 62, 78, 35, 75, 184, "jiao_zhi",
        ["long_rule"], 26, "qun_admin_reform", "qun_regional_lord", "archer", 72)
    add("shi_yi", "士壹", "士燮之弟", 45, 52, 52, 48, 48, 72, 184, "jiao_zhi",
        [], 18, "qun_steady_strike", "qun_kinsman_general", "vanguard", 42)
    add("shi_hui", "士徽", "士燮之子", 42, 48, 45, 40, 50, 60, 220, "jiao_zhi",
        [], 16, "", "qun_rebellious_son", "vanguard", 35)
    add("bu_zhi", "步骘", "东吴名臣", 38, 75, 62, 78, 42, 85, 194, "huai_yin",
        [], 26, "qun_admin_reform", "qun_capable_minister", "archer", 70)
    add("yan_jun", "严畯", "东吴文臣", 22, 70, 35, 68, 38, 80, 194, "peng_cheng",
        [], 20, "qun_admin_reform", "qun_scholar_official", "archer", 62)
    add("cheng_bing", "程秉", "东吴文臣", 20, 72, 30, 65, 35, 78, 184, "ru_nan",
        [], 20, "qun_literary_fame", "qun_scholar_official", "archer", 60)
    add("zhao_da", "赵达", "东吴术士", 25, 68, 35, 52, 42, 72, 194, "lu_jiang",
        ["calculation"], 18, "qun_strategic_plan", "qun_calculator", "archer", 52)
    add("lu_su2", "阚泽", "东吴学者", 22, 78, 35, 72, 40, 82, 194, "kuai_ji",
        [], 22, "qun_literary_fame", "qun_scholar", "archer", 65)
    add("wu_can", "吾粲", "东吴文臣", 30, 70, 45, 72, 42, 80, 200, "yu_zhang",
        [], 22, "qun_admin_reform", "qun_honest_official", "archer", 65)
    add("tanggu", "唐固", "东吴学者", 20, 74, 28, 62, 35, 75, 194, "dan_yang",
        [], 20, "qun_literary_fame", "qun_scholar", "archer", 58)
    add("zhang_wen_dongwu", "张温", "东吴才子", 28, 78, 42, 65, 45, 70, 200, "wu_jun",
        [], 22, "qun_literary_fame", "qun_brilliant_scholar", "archer", 65)
    add("lu_mao", "陆茂", "东吴宗室", 38, 52, 45, 42, 48, 72, 200, "wu_county",
        [], 16, "", "qun_minor_noble", "vanguard", 38)

    # 东吴武将
    add("sun_he", "孙和", "东吴太子", 42, 65, 48, 52, 45, 75, 228, "jian_ye",
        [], 22, "", "qun_tragic_prince", "archer", 62)
    add("sun_ba", "孙霸", "鲁王", 40, 52, 42, 45, 48, 65, 228, "jian_ye",
        ["ambitious_prince"], 20, "", "qun_ambitious_prince", "archer", 48)
    add("sun_fen", "孙奋", "齐王", 38, 48, 40, 42, 48, 68, 228, "jian_ye",
        [], 18, "", "qun_minor_prince", "archer", 42)
    add("sun_xiu", "孙休", "吴景帝", 42, 68, 52, 62, 45, 80, 240, "kuai_ji",
        [], 26, "qun_imperial_mandate", "qun_restoring_emperor", "archer", 62)
    add("sun_hao", "孙皓", "吴末帝", 48, 58, 52, 42, 50, 55, 260, "jian_ye",
        ["tyrant_nature"], 24, "qun_tyrant_fury", "qun_cruel_emperor", "archer", 38)
    add("sun_jing", "孙静", "孙坚之弟", 55, 58, 58, 52, 52, 80, 184, "fu_chun",
        [], 22, "", "qun_loyal_kinsman", "archer", 52)
    add("sun_gao", "孙贲", "孙坚之侄", 62, 52, 58, 45, 58, 78, 184, "yu_zhang",
        [], 22, "", "qun_kinsman_general", "vanguard", 45)
    add("sun_fu", "孙辅", "孙坚之侄", 58, 55, 55, 48, 55, 75, 184, "lu_jiang",
        [], 20, "", "qun_kinsman_general", "vanguard", 42)
    add("sun_yi", "孙翊", "孙坚之子", 72, 42, 58, 38, 72, 80, 184, "fu_chun",
        [], 22, "qun_fierce_strike", "qun_brave_prince", "vanguard", 45)
    add("sun_kuang", "孙匡", "孙坚之子", 42, 48, 40, 42, 45, 82, 184, "fu_chun",
        [], 16, "", "qun_minor_prince", "archer", 42)
    add("sun_shao", "孙韶", "东吴宗室", 65, 58, 68, 52, 60, 82, 200, "wu_jun",
        [], 24, "qun_steady_strike", "qun_reliable_kinsman", "vanguard", 50)
    add("sun_yi2", "孙异", "东吴将领", 58, 52, 55, 45, 55, 78, 228, "jian_ye",
        [], 18, "", "qun_reliable_general", "vanguard", 42)
    add("sun_kai", "孙楷", "东吴宗室", 52, 55, 52, 48, 52, 75, 228, "jian_ye",
        [], 18, "", "qun_minor_noble", "archer", 45)

    # 东吴武将补充
    add("zuo_rui", "左咸", "东吴将领", 55, 48, 52, 42, 52, 72, 228, "jian_ye",
        [], 18, "", "qun_reliable_general", "vanguard", 40)
    add("pan_chang", "潘璋", "东吴猛将", 82, 42, 72, 35, 75, 75, 194, "fa_gan",
        ["capturing_general"], 26, "qun_fierce_strike", "qun_capturer", "vanguard", 38)
    add("jiang_qin", "蒋钦", "东吴十二虎臣", 80, 52, 75, 42, 72, 82, 194, "shou_chun",
        [], 26, "qun_steady_strike", "qun_tiger_minister", "vanguard", 45)
    add("chen_wu", "陈武", "东吴猛将", 82, 45, 72, 38, 72, 85, 194, "lu_jiang",
        [], 24, "qun_fierce_strike", "qun_brave_guard", "vanguard", 42)
    add("dongxi", "董袭", "东吴猛将", 78, 42, 68, 35, 70, 85, 194, "kuai_ji",
        [], 22, "qun_fierce_strike", "qun_brave_guard", "vanguard", 38)
    add("xu_sheng", "徐盛", "东吴名将", 80, 65, 82, 52, 68, 82, 194, "lang_ya",
        ["river_defense"], 28, "qun_steady_strike", "qun_defensive_master", "vanguard", 52)
    add("ding_feng", "丁奉", "东吴老将", 78, 55, 75, 48, 72, 80, 200, "lu_an",
        [], 26, "qun_fierce_strike", "qun_veteran_hero", "vanguard", 48)
    add("han_dang", "韩当", "东吴三朝老将", 82, 52, 78, 42, 72, 88, 184, "liao_xi",
        [], 26, "qun_steady_strike", "qun_three_dynasty_veteran", "vanguard", 45)
    add("zhu_zhi", "朱治", "东吴元老", 65, 62, 72, 58, 58, 90, 184, "yang_di",
        [], 24, "qun_steady_strike", "qun_veteran_minister", "vanguard", 58)
    add("zhu_huan", "朱桓", "东吴名将", 82, 68, 80, 52, 72, 80, 200, "wu_jun",
        [], 28, "qun_steady_strike", "qun_proud_general", "vanguard", 55)
    add("zhu_yi", "朱异", "朱桓之子", 72, 55, 68, 45, 65, 75, 228, "wu_jun",
        [], 22, "qun_steady_strike", "qun_reliable_general", "vanguard", 45)
    add("sun_chen", "孙綝", "东吴权臣", 58, 55, 58, 48, 55, 50, 240, "jian_ye",
        ["usurper"], 24, "qun_usurper", "qun_arrogant_regent", "archer", 38)
    add("quancong", "全琮", "东吴名将", 72, 62, 75, 58, 65, 82, 200, "qian_tang",
        [], 26, "qun_steady_strike", "qun_reliable_general", "vanguard", 55)
    add("quan_yi", "全怿", "全琮之子", 55, 52, 52, 45, 52, 68, 228, "qian_tang",
        [], 18, "", "qun_minor_noble", "vanguard", 38)
    add("quan_duan", "全端", "全琮之子", 52, 50, 48, 42, 50, 68, 228, "qian_tang",
        [], 18, "", "qun_minor_noble", "vanguard", 35)
    add("zhu_ju", "朱据", "东吴驸马", 62, 58, 58, 52, 55, 75, 200, "wu_jun",
        [], 22, "", "qun_royal_kinsman", "archer", 52)
    add("lu_kang", "陆抗", "东吴名将", 68, 80, 85, 72, 60, 88, 240, "jian_ye",
        ["river_defense_master"], 32, "qun_steady_strike", "qun_last_pillar", "archer", 75)
    add("lu_yan", "陆晏", "陆抗之子", 55, 52, 52, 42, 55, 80, 260, "jian_ye",
        [], 20, "qun_steady_strike", "qun_loyal_son", "vanguard", 45)
    add("lu_jing", "陆景", "陆抗之子", 52, 58, 48, 45, 55, 80, 260, "jian_ye",
        [], 20, "", "qun_loyal_son", "archer", 42)
    add("sun_yan", "孙研", "东吴将领", 62, 48, 55, 40, 60, 75, 240, "jian_ye",
        [], 18, "", "qun_reliable_general", "vanguard", 38)
    add("zhang_bu_dongwu", "张布", "东吴将领", 65, 55, 60, 48, 58, 75, 240, "dan_yang",
        [], 20, "", "qun_reliable_general", "vanguard", 45)
    add("dianji", "典农", "东吴屯田官", 35, 62, 45, 72, 40, 78, 200, "lu_jiang",
        [], 22, "qun_admin_reform", "qun_agriculture_official", "archer", 55)

    # ================================================================
    # 第四类：群雄 / 地方势力
    # ================================================================
    add("zhang_jue2", "张宝", "黄巾领袖", 55, 55, 58, 35, 55, 95, 184, "ju_lu",
        ["yellow_magic"], 24, "qun_dark_magic", "qun_yellow_turban", "archer", 40)
    add("zhang_liang", "张梁", "黄巾领袖", 60, 50, 55, 30, 60, 95, 184, "ju_lu",
        [], 22, "qun_dark_magic", "qun_yellow_turban", "vanguard", 35)
    add("zhang_man_cheng", "张曼成", "黄巾渠帅", 65, 32, 48, 22, 58, 88, 184, "nan_yang",
        [], 18, "qun_mob_charge", "qun_yellow_turban", "vanguard", 28)
    add("zhao_hong", "赵弘", "黄巾渠帅", 62, 30, 45, 20, 55, 88, 184, "nan_yang",
        [], 16, "qun_mob_charge", "qun_yellow_turban", "vanguard", 25)
    add("han_zhong_hj", "韩忠", "黄巾渠帅", 58, 28, 42, 18, 55, 88, 184, "nan_yang",
        [], 16, "qun_mob_charge", "qun_yellow_turban", "vanguard", 22)
    add("sun_xia", "孙夏", "黄巾渠帅", 55, 30, 40, 20, 58, 88, 184, "nan_yang",
        [], 15, "qun_mob_charge", "qun_yellow_turban", "vanguard", 22)
    add("bo_cai", "波才", "黄巾渠帅", 60, 35, 50, 25, 58, 88, 184, "ying_chuan",
        [], 18, "qun_mob_charge", "qun_yellow_turban", "vanguard", 28)
    add("peng_tuo", "彭脱", "黄巾渠帅", 55, 28, 42, 22, 55, 88, 184, "ru_nan",
        [], 15, "qun_mob_charge", "qun_yellow_turban", "vanguard", 22)

    add("dai_shan", "戴山", "山贼", 58, 28, 40, 18, 58, 50, 184, "tai_shan",
        [], 15, "qun_bandit_raid", "qun_mountain_bandit", "vanguard", 22)
    add("zhang_yan", "张燕", "黑山贼帅", 72, 42, 62, 35, 68, 58, 184, "chang_shan",
        ["black_mountain"], 24, "qun_bandit_command", "qun_black_mountain_chief", "vanguard", 35)
    add("yu_du_luo", "于毒", "黑山贼", 58, 28, 45, 22, 55, 55, 184, "dong_jun",
        [], 16, "qun_bandit_raid", "qun_black_mountain", "vanguard", 25)
    add("bai_rao", "白绕", "黑山贼", 55, 25, 42, 20, 55, 55, 184, "dong_jun",
        [], 15, "qun_bandit_raid", "qun_black_mountain", "vanguard", 22)
    add("suo_luo", "眭固", "黑山贼", 68, 32, 55, 25, 65, 58, 184, "he_nei",
        [], 18, "qun_bandit_raid", "qun_black_mountain", "vanguard", 28)

    # 十常侍
    add("zhang_rang", "张让", "十常侍之首", 12, 55, 22, 52, 35, 85, 184, "luo_yang",
        ["eunuch_power", "court_manipulation"], 22, "qun_court_power", "qun_eunuch_chief", "archer", 32)
    add("zhao_zhong", "赵忠", "十常侍", 15, 52, 25, 48, 35, 85, 184, "luo_yang",
        ["eunuch_power"], 20, "qun_court_power", "qun_eunuch", "archer", 28)
    add("jian_shuo", "蹇硕", "上军校尉", 48, 45, 42, 35, 48, 80, 184, "luo_yang",
        [], 18, "", "qun_eunuch_general", "vanguard", 30)
    add("bi_lan", "毕岚", "十常侍", 12, 48, 20, 45, 32, 82, 184, "luo_yang",
        [], 18, "qun_court_power", "qun_eunuch", "archer", 25)
    add("guo_sheng", "郭胜", "十常侍", 12, 50, 22, 48, 32, 82, 184, "luo_yang",
        [], 18, "qun_court_power", "qun_eunuch", "archer", 25)
    add("song_dian", "宋典", "十常侍", 12, 45, 18, 42, 30, 82, 184, "luo_yang",
        [], 16, "qun_court_power", "qun_eunuch", "archer", 22)
    add("xia_hui", "夏恽", "十常侍", 12, 42, 18, 40, 30, 82, 184, "luo_yang",
        [], 16, "qun_court_power", "qun_eunuch", "archer", 22)
    add("gong_le", "恭乐", "十常侍", 12, 40, 15, 38, 28, 82, 184, "luo_yang",
        [], 15, "qun_court_power", "qun_eunuch", "archer", 20)
    add("jian_shuo2", "段珪", "十常侍", 12, 42, 18, 40, 30, 82, 184, "luo_yang",
        [], 16, "qun_court_power", "qun_eunuch", "archer", 22)

    add("dong_cheng", "董承", "国舅", 55, 58, 58, 48, 52, 85, 194, "luo_yang",
        ["secret_decree"], 24, "qun_secret_decree", "qun_imperial_kinsman", "vanguard", 52)
    add("mao_jie", "毛玠", "曹魏选官", 28, 72, 48, 78, 40, 88, 194, "chen_liu",
        ["talent_selection"], 24, "qun_admin_reform", "qun_talent_scout", "archer", 68)
    add("han_dang2", "韩馥", "冀州牧", 38, 62, 58, 65, 42, 65, 184, "ye_cheng",
        ["cowardly_nature"], 22, "qun_passive_aura", "qun_timid_governor", "archer", 52)
    add("kong_zhou", "孔伷", "豫州刺史", 32, 58, 48, 55, 40, 68, 184, "ru_nan",
        [], 18, "", "qun_provincial_governor", "archer", 45)
    add("zhang_zi", "张咨", "南阳太守", 35, 48, 42, 45, 42, 65, 184, "wan_cheng",
        [], 16, "", "qun_minor_governor", "archer", 38)

    # 公孙瓒部下
    add("tian_yu_gc", "田豫", "公孙瓒部将", 68, 65, 72, 55, 68, 82, 194, "you_zhou",
        [], 24, "qun_steady_strike", "qun_northern_general", "vanguard", 55)
    add("qi_su", "齐宿", "公孙瓒部将", 55, 48, 52, 42, 55, 75, 194, "you_zhou",
        [], 18, "", "qun_reliable_general", "vanguard", 38)
    add("fan_ming", "范明", "公孙瓒部将", 52, 45, 50, 40, 52, 75, 194, "you_zhou",
        [], 16, "", "qun_reliable_general", "vanguard", 35)
    add("dan_jing", "单经", "公孙瓒部将", 60, 48, 58, 42, 58, 78, 194, "ping_yuan",
        [], 20, "", "qun_reliable_general", "vanguard", 42)

    # 韩遂部下
    add("han_sui2", "侯选", "韩遂部将", 58, 38, 52, 32, 58, 65, 194, "jin_cheng",
        [], 16, "", "qun_rebel_general", "cavalry", 32)
    add("cheng_yin", "程银", "韩遂部将", 55, 35, 50, 30, 55, 62, 194, "jin_cheng",
        [], 15, "", "qun_rebel_general", "cavalry", 28)
    add("li_kan", "李堪", "韩遂部将", 58, 32, 52, 28, 60, 60, 194, "jin_cheng",
        [], 15, "", "qun_rebel_general", "cavalry", 28)
    add("mao_wan", "马玩", "韩遂部将", 62, 30, 55, 25, 65, 58, 194, "jin_cheng",
        [], 16, "", "qun_rebel_general", "cavalry", 30)
    add("wang_fang", "王方", "韩遂部将", 55, 28, 48, 22, 55, 58, 194, "chang_an",
        [], 14, "", "qun_rebel_general", "cavalry", 25)
    add("zhang_heng_wei", "张横", "韩遂部将", 60, 30, 52, 25, 60, 55, 194, "jin_cheng",
        [], 16, "", "qun_rebel_general", "cavalry", 28)
    add("cheng_kuang", "成宜", "韩遂部将", 58, 32, 50, 28, 58, 58, 194, "jin_cheng",
        [], 15, "", "qun_rebel_general", "cavalry", 28)

    # 刘表部下
    add("kuai_liang", "蒯良", "刘表谋士", 25, 82, 48, 78, 42, 80, 184, "xiang_yang",
        ["strategic_vision"], 26, "qun_strategic_plan", "qun_wise_counselor", "archer", 68)
    add("kuai_yue", "蒯越", "刘表谋士", 28, 78, 52, 75, 45, 75, 184, "xiang_yang",
        [], 24, "qun_strategic_plan", "qun_cunning_scholar", "archer", 62)
    add("cai_mao", "蔡瑁", "刘表妻弟", 62, 55, 65, 52, 55, 65, 184, "xiang_yang",
        ["water_command"], 24, "qun_river_defense", "qun_naval_commander", "archer", 48)
    add("wen_pin", "文聘", "刘表部将", 78, 58, 80, 48, 65, 88, 184, "xin_ye",
        [], 26, "qun_steady_strike", "qun_loyal_general", "vanguard", 52)
    add("huang_zu", "黄祖", "刘表部将", 68, 48, 62, 38, 60, 78, 184, "xia_kou",
        [], 22, "qun_river_defense", "qun_regional_commander", "vanguard", 42)
    add("deng_xi", "邓熙", "刘表部将", 55, 52, 55, 48, 52, 78, 194, "xiang_yang",
        [], 18, "", "qun_reliable_general", "vanguard", 42)
    add("wei_feng", "魏讽", "刘表部将", 52, 55, 50, 45, 50, 75, 194, "xiang_yang",
        [], 18, "", "qun_reliable_general", "archer", 40)
    add("su_fei", "苏飞", "刘表部将", 60, 52, 58, 42, 58, 75, 194, "xia_kou",
        [], 20, "", "qun_reliable_general", "vanguard", 42)
    add("liu_xian", "刘贤", "刘表之子", 35, 48, 38, 42, 42, 72, 184, "ling_ling",
        [], 15, "", "qun_minor_prince", "archer", 35)

    # 刘璋部下
    add("fa_zheng2", "法真", "法正之父", 22, 75, 30, 68, 35, 82, 184, "you_fu_ling",
        [], 22, "qun_literary_fame", "qun_scholar", "archer", 65)
    add("leng_bao", "冷苞", "刘璋部将", 58, 38, 52, 32, 55, 72, 194, "zi_tong",
        [], 16, "", "qun_regional_defender", "vanguard", 32)
    add("huang_yuan_lc", "黄元", "刘璋部将", 55, 42, 50, 35, 55, 68, 194, "han_jia",
        [], 16, "", "qun_regional_defender", "vanguard", 30)
    add("wu_yi_lc", "吴兰", "刘璋部将", 62, 38, 55, 32, 58, 72, 194, "ba_xi",
        [], 18, "", "qun_regional_defender", "vanguard", 32)
    add("lei_tong_lc", "雷铜", "刘璋部将", 60, 35, 52, 30, 62, 70, 194, "ba_xi",
        [], 16, "", "qun_regional_defender", "vanguard", 28)
    add("dan_qiong", "耽琼", "刘璋部将", 48, 35, 42, 28, 50, 68, 194, "cheng_du",
        [], 14, "", "qun_regional_defender", "vanguard", 25)
    add("meng_da2", "孟达", "反复之臣", 68, 72, 70, 62, 68, 35, 194, "xiang_yang",
        ["turncoat"], 24, "qun_turncoat", "qun_opportunist", "vanguard", 42)
    add("huang_quan_lc", "黄权", "蜀中名将", 45, 82, 75, 78, 48, 78, 194, "cheng_du",
        [], 28, "qun_strategic_withdraw", "qun_strategic_foresight", "archer", 62)

    # 袁绍部下补充
    add("shen_pei", "审配", "袁绍忠臣", 52, 78, 68, 62, 55, 95, 184, "ye_cheng",
        ["unyielding_loyalty"], 28, "qun_unyielding_siege", "qun_loyal_minister", "archer", 72)
    add("guo_tu", "郭图", "袁绍谋士", 28, 72, 48, 58, 42, 65, 184, "ying_chuan",
        ["scheming"], 22, "qun_scheming", "qun_cunning_scholar", "archer", 48)
    add("xin_ping", "辛评", "袁绍谋士", 25, 70, 42, 62, 40, 80, 184, "ye_cheng",
        [], 20, "", "qun_scholar_official", "archer", 55)
    add("feng_ji", "逢纪", "袁绍谋士", 30, 68, 45, 55, 45, 70, 184, "nan_yang",
        [], 20, "", "qun_scholar_official", "archer", 50)
    add("lv_kuang", "吕旷", "袁绍部将", 68, 42, 58, 35, 65, 65, 194, "ye_cheng",
        [], 20, "", "qun_reliable_general", "vanguard", 38)
    add("lv_xiang", "吕翔", "袁绍部将", 65, 40, 55, 32, 62, 65, 194, "ye_cheng",
        [], 18, "", "qun_reliable_general", "vanguard", 35)
    add("mao_jie2", "马延", "袁绍部将", 62, 42, 58, 35, 60, 68, 194, "ye_cheng",
        [], 18, "", "qun_reliable_general", "vanguard", 38)
    add("zhang_kai", "张顗", "袁绍部将", 60, 40, 55, 32, 58, 68, 194, "ye_cheng",
        [], 18, "", "qun_reliable_general", "vanguard", 35)
    add("zhao_rui", "赵睿", "袁绍部将", 58, 38, 52, 30, 58, 65, 194, "ye_cheng",
        [], 16, "", "qun_reliable_general", "vanguard", 32)
    add("han_meng", "韩猛", "袁绍部将", 75, 38, 62, 30, 72, 75, 184, "ye_cheng",
        [], 22, "qun_fierce_strike", "qun_bold_general", "vanguard", 35)

    # 吕布部下补充
    add("song_xian", "宋宪", "吕布部将", 68, 38, 55, 30, 60, 55, 189, "xia_pi",
        [], 18, "", "qun_reliable_general", "vanguard", 32)
    add("wei_xu", "魏续", "吕布部将", 65, 35, 52, 28, 58, 55, 189, "xia_pi",
        [], 16, "", "qun_reliable_general", "vanguard", 28)
    add("hou_cheng", "侯成", "吕布部将", 70, 40, 58, 32, 62, 52, 189, "xia_pi",
        [], 18, "", "qun_reliable_general", "vanguard", 32)
    add("chen_gong2", "陈宫", "刚直壮士", 55, 88, 80, 72, 58, 75, 184, "dong_jun",
        [], 28, "qun_counter_plot", "qun_upright_warrior", "archer", 65)
    add("zhang_liao2", "张辽", "威震逍遥津", 88, 68, 85, 65, 85, 80, 184, "yan_province",
        [], 32, "qun_ambush_charge", "qun_vanguard_spirit", "cavalry", 55)
    add("gao_shun", "高顺", "陷阵都督", 85, 58, 82, 42, 68, 92, 189, "xia_pi",
        ["formation_command", "loyal_to_end"], 30, "qun_formation_strike", "qun_formation_master", "vanguard", 52)
    add("cao_xing", "曹性", "吕布部将", 72, 42, 58, 35, 65, 78, 189, "he_dong",
        ["archer_skill"], 20, "qun_piercing_arrow", "qun_skilled_archer", "archer", 38)

    # ================================================================
    # 第五类：特殊人物（隐士、方士、医者等）
    # ================================================================
    add("hua_tuo", "华佗", "神医", 25, 78, 22, 72, 38, 85, 184, "pei_guo",
        ["healing_art", "surgery"], 28, "qun_healing_art", "qun_divine_physician", "archer", 88)
    add("zuo_ci", "左慈", "方士", 32, 85, 28, 55, 48, 70, 184, "lu_jiang",
        ["magic_transformation", "illusion"], 28, "qun_illusion", "qun_magic_master", "archer", 82)
    add("yu_ji", "于吉", "太平道", 28, 82, 32, 58, 42, 75, 184, "lang_ya",
        ["taoist_magic", "heal"], 26, "qun_dark_magic", "qun_taoist_sage", "archer", 80)
    add("gan_shi", "干吉", "太平道", 22, 78, 25, 52, 38, 72, 184, "kuai_ji",
        ["taoist_magic"], 24, "qun_dark_magic", "qun_taoist_priest", "archer", 75)
    add("nan_hua_lao", "南华老仙", "仙人", 20, 92, 18, 45, 55, 90, 184, "ju_lu",
        ["heaven_magic", "earth_magic"], 30, "qun_divine_magic", "qun_immortal_sage", "archer", 95)
    add("huangfu_mi", "皇甫谧", "医学家", 18, 75, 20, 65, 35, 78, 200, "an_ding",
        [], 22, "qun_healing_art", "qun_medical_scholar", "archer", 72)
    add("zhang_zhong_jing", "张仲景", "医圣", 15, 82, 18, 70, 32, 82, 184, "nan_yang",
        ["medical_classic"], 26, "qun_healing_art", "qun_medical_sage", "archer", 85)
    add("pu_yuan", "蒲元", "铸刀师", 32, 72, 28, 62, 45, 75, 200, "cheng_du",
        ["blade_forging"], 24, "qun_blade_forging", "qun_master_smith", "vanguard", 65)

    # ================================================================
    # 第六类：少数民族武将
    # ================================================================
    add("ke_bui_neng", "轲比能", "鲜卑首领", 85, 48, 72, 35, 82, 70, 200, "dai_jun",
        ["nomad_charge"], 28, "qun_nomad_charge", "qun_xianbei_chief", "cavalry", 42)
    add("budugen", "步度根", "鲜卑首领", 78, 42, 65, 30, 78, 65, 200, "yun_zhong",
        [], 24, "qun_nomad_charge", "qun_xianbei_leader", "cavalry", 38)
    add("suiwang", "素利", "鲜卑首领", 72, 38, 58, 28, 72, 62, 200, "shang_gu",
        [], 22, "qun_nomad_charge", "qun_xianbei_leader", "cavalry", 35)
    add("milan_xb", "弥加", "鲜卑首领", 70, 35, 55, 25, 70, 58, 200, "bei_ping",
        [], 20, "qun_nomad_charge", "qun_xianbei_leader", "cavalry", 32)
    add("mohanba", "莫护跋", "鲜卑首领", 75, 40, 62, 30, 75, 60, 220, "liao_xi",
        [], 22, "qun_nomad_charge", "qun_xianbei_leader", "cavalry", 35)
    add("wuyan_xb", "屋炎", "鲜卑首领", 68, 35, 52, 25, 68, 55, 220, "dai_jun",
        [], 20, "qun_nomad_charge", "qun_xianbei_leader", "cavalry", 30)

    add("qi_li_jian", "七离箭", "羌族勇士", 78, 32, 55, 22, 78, 65, 184, "an_ding",
        [], 22, "qun_piercing_arrow", "qun_qiang_warrior", "cavalry", 32)
    add("tang_ti", "唐蹄", "羌族首领", 82, 38, 62, 28, 75, 68, 194, "wu_wei",
        [], 24, "qun_nomad_charge", "qun_qiang_chief", "cavalry", 38)
    add("e_shao", "娥烧", "羌族首领", 75, 35, 58, 25, 72, 62, 194, "an_ding",
        [], 22, "qun_nomad_charge", "qun_qiang_leader", "cavalry", 32)
    add("mi_dang", "迷当", "羌族首领", 72, 32, 55, 22, 70, 58, 207, "wu_wei",
        [], 20, "qun_nomad_charge", "qun_qiang_leader", "cavalry", 30)
    add("mi_jia", "迷姜", "羌族首领", 70, 30, 52, 20, 68, 55, 207, "wu_wei",
        [], 18, "qun_nomad_charge", "qun_qiang_leader", "cavalry", 28)
    add("yue_ji", "越吉", "羌族元帅", 88, 35, 65, 25, 72, 70, 225, "yun_nan",
        ["iron_chariot"], 28, "qun_iron_charge", "qun_iron_chariot_commander", "cavalry", 38)
    add("ya_dan", "雅丹", "羌族丞相", 38, 72, 52, 58, 42, 75, 225, "yun_nan",
        [], 22, "qun_strategic_plan", "qun_tribal_minister", "archer", 52)

    add("shan_yue_1", "山越大帅", "山越首领", 72, 32, 55, 22, 70, 65, 194, "yu_zhang",
        [], 20, "qun_bandit_raid", "qun_shanyue_chief", "vanguard", 30)
    add("shi_yuan2", "斯由", "山越首领", 68, 30, 52, 20, 68, 60, 194, "dan_yang",
        [], 18, "qun_bandit_raid", "qun_shanyue_leader", "vanguard", 28)
    add("qian_xuan", "钱铜", "山越首领", 65, 28, 48, 18, 65, 58, 194, "kuai_ji",
        [], 16, "qun_bandit_raid", "qun_shanyue_leader", "vanguard", 25)
    add("zhu_zhi_sy", "朱治", "山越首领", 62, 32, 50, 22, 62, 55, 200, "wu_jun",
        [], 16, "qun_bandit_raid", "qun_shanyue_leader", "vanguard", 28)

    add("wulan_nm", "乌兰", "南蛮酋长", 75, 28, 55, 20, 72, 65, 200, "yun_nan",
        [], 22, "qun_beast_charge", "qun_nan_chief", "vanguard", 32)
    add("da_lai", "带来", "南蛮勇士", 78, 32, 58, 22, 75, 68, 225, "yun_nan",
        [], 24, "qun_beast_charge", "qun_nan_warrior", "vanguard", 35)
    add("meng_you2", "孟优", "孟获之弟", 65, 42, 52, 30, 65, 72, 225, "yun_nan",
        [], 20, "", "qun_nan_kinsman", "vanguard", 32)
    add("dong_tu_na", "董荼那", "南蛮元帅", 78, 28, 55, 18, 68, 70, 225, "yun_nan",
        [], 22, "qun_beast_charge", "qun_nan_commander", "vanguard", 30)
    add("a_hui_nan", "阿会喃", "南蛮元帅", 75, 30, 52, 20, 72, 68, 225, "yun_nan",
        [], 22, "qun_beast_charge", "qun_nan_commander", "vanguard", 32)
    add("mang_ya_chang", "忙牙长", "南蛮将领", 72, 25, 50, 18, 70, 65, 225, "yun_nan",
        [], 20, "qun_beast_charge", "qun_nan_general", "vanguard", 28)
    add("jin_huan_san_jie", "金环三结", "南蛮大将", 70, 28, 48, 18, 68, 62, 225, "yun_nan",
        [], 20, "qun_beast_charge", "qun_nan_general", "vanguard", 28)

    # ================================================================
    # 第七类：三国后期人物
    # ================================================================
    add("yang_hu", "羊祜", "晋国名将", 52, 78, 82, 80, 48, 85, 260, "cheng_du",
        ["benevolent_rule_jin"], 32, "qun_steady_strike", "qun_benevolent_commander", "archer", 82)
    add("du_yu", "杜预", "晋国名将", 58, 85, 82, 82, 48, 82, 260, "jing_zhao",
        ["scholarly_general"], 34, "qun_steady_strike", "qun_scholarly_commander", "archer", 78)
    add("wang_jun_jin", "王浚", "晋国水师", 62, 72, 78, 65, 55, 80, 260, "hong_nong",
        ["naval_command"], 30, "qun_naval_command", "qun_admiral", "archer", 62)
    add("tang_bin", "唐彬", "晋国将领", 58, 68, 72, 62, 55, 78, 260, "lu_jiang",
        [], 26, "qun_steady_strike", "qun_reliable_commander", "vanguard", 58)
    add("hu_fen", "胡奋", "晋国将领", 68, 55, 68, 48, 62, 78, 260, "an_ding",
        [], 24, "qun_steady_strike", "qun_veteran_general", "vanguard", 52)
    add("wang_rong_jin", "王戎", "竹林七贤", 28, 72, 38, 65, 42, 65, 250, "lang_ya",
        [], 22, "qun_literary_fame", "qun_seven_sages", "archer", 58)
    add("shan_tao", "山涛", "竹林七贤", 22, 75, 32, 72, 38, 72, 250, "he_nei",
        [], 24, "qun_literary_fame", "qun_seven_sages", "archer", 65)
    add("xi_kang", "嵇康", "竹林七贤", 32, 78, 28, 58, 45, 60, 240, "qiao",
        ["music_mastery"], 22, "qun_literary_fame", "qun_seven_sages", "archer", 72)
    add("ruan_ji", "阮籍", "竹林七贤", 25, 80, 22, 55, 40, 55, 240, "chen_liu",
        ["wine_poetry"], 20, "qun_literary_fame", "qun_seven_sages", "archer", 68)
    add("liu_ling", "刘伶", "竹林七贤", 18, 68, 15, 42, 35, 50, 240, "pei_guo",
        [], 16, "qun_literary_fame", "qun_seven_sages", "archer", 55)
    add("xiang_xiu", "向秀", "竹林七贤", 20, 75, 22, 58, 38, 58, 250, "he_nei",
        [], 18, "qun_literary_fame", "qun_seven_sages", "archer", 60)
    add("wang_yan_jin", "王衍", "清谈名士", 18, 75, 22, 62, 38, 55, 260, "lang_ya",
        [], 20, "qun_literary_fame", "qun_metaphysical_scholar", "archer", 55)
    add("yue_guang", "乐广", "清谈名士", 15, 78, 20, 58, 35, 58, 260, "nan_yang",
        [], 20, "qun_literary_fame", "qun_metaphysical_scholar", "archer", 58)

    add("zhuo_tao", "卓韬", "蜀汉降将", 55, 48, 50, 42, 52, 55, 260, "cheng_du",
        [], 18, "", "qun_surrender_general", "vanguard", 35)
    add("jiao_zhou2", "谯周2", "劝降名臣", 18, 82, 30, 70, 35, 55, 260, "ba_xi",
        ["persuasion"], 24, "qun_persuasion", "qun_surrender_advocate", "archer", 52)

    # 晋国宗室
    add("simayan", "司马炎", "晋武帝", 55, 72, 78, 75, 50, 85, 260, "luo_yang",
        ["imperial_foundation"], 36, "qun_imperial_mandate", "qun_founding_emperor", "archer", 78)
    add("simazhao2", "司马昭", "晋文帝", 62, 82, 85, 78, 52, 80, 240, "luo_yang",
        ["regent_power"], 38, "qun_imperial_mandate", "qun_usurper_father", "archer", 72)
    add("simashi", "司马师", "晋景帝", 58, 78, 82, 75, 50, 85, 228, "luo_yang",
        ["steady_rule"], 36, "qun_imperial_mandate", "qun_steady_regent", "archer", 75)

    # ================================================================
    # 第八类：更多次要人物 / 文臣武将
    # ================================================================

    # 更多文臣
    add("fu_jia", "傅嘏", "魏国名臣", 28, 75, 45, 78, 42, 82, 200, "bei_di",
        [], 24, "qun_admin_reform", "qun_capable_minister", "archer", 68)
    add("li_feng_wei", "李丰", "魏国名臣", 32, 72, 48, 75, 42, 82, 200, "nan_yang",
        [], 24, "qun_admin_reform", "qun_trusted_minister", "archer", 65)
    add("zhang_ji_wei", "张缉", "魏国名臣", 30, 68, 45, 72, 40, 78, 228, "hong_nong",
        [], 22, "qun_admin_reform", "qun_honest_official", "archer", 60)
    add("xiahou_xuan", "夏侯玄", "魏国名士", 48, 78, 52, 65, 48, 72, 220, "qiao",
        ["elegant_scholar"], 26, "qun_literary_fame", "qun_elegant_scholar", "archer", 72)
    add("li_sheng2", "李胜", "魏国官员", 30, 62, 40, 58, 42, 60, 228, "nan_yang",
        [], 18, "", "qun_minor_official", "archer", 48)
    add("xu_yun", "许允", "魏国官员", 28, 68, 38, 62, 40, 65, 200, "gao_yang",
        [], 20, "", "qun_minor_official", "archer", 52)
    add("xiahou_hui", "夏侯徽", "司马懿妻", 22, 72, 25, 55, 38, 75, 200, "qiao",
        [], 18, "", "qun_noble_woman", "archer", 68)

    add("zhuge_ke", "诸葛恪", "东吴权臣", 52, 82, 75, 68, 50, 70, 228, "lang_ya",
        ["arrogant_strategy"], 30, "qun_strategic_plan", "qun_arrogant_regent", "archer", 58)
    add("sun_jun", "孙峻", "东吴权臣", 55, 52, 58, 42, 55, 50, 240, "kuai_ji",
        ["assassination"], 22, "qun_assassination", "qun_usurper", "vanguard", 35)
    add("sun_lin", "孙綝", "东吴权臣", 58, 55, 60, 45, 55, 48, 240, "kuai_ji",
        [], 24, "qun_usurper", "qun_arrogant_regent", "vanguard", 38)
    add("zhang_ti", "张悌", "东吴丞相", 35, 75, 58, 72, 42, 85, 260, "ru_nan",
        [], 26, "qun_strategic_plan", "qun_loyal_prime_minister", "archer", 72)
    add("shen_ying", "沈莹", "东吴将领", 72, 55, 68, 42, 65, 82, 260, "ru_jiang",
        ["water_army"], 24, "qun_steady_strike", "qun_naval_commander", "vanguard", 52)
    add("sun_zhen", "孙震", "东吴宗室", 55, 52, 52, 45, 55, 75, 260, "jian_ye",
        [], 20, "", "qun_minor_noble", "vanguard", 42)

    add("huangfu_song2", "皇甫嵩", "名将", 68, 80, 88, 72, 65, 92, 184, "chang_an",
        [], 35, "qun_fire_assault", "qun_decisive_command", "archer", 75)

    # 更多武将
    add("zhang_yun", "张允", "蔡瑁副将", 62, 48, 55, 38, 55, 65, 184, "xiang_yang",
        [], 18, "", "qun_deputy_general", "vanguard", 35)
    add("cai_xun", "蔡勋", "蔡瑁之族", 55, 42, 48, 35, 52, 62, 184, "xiang_yang",
        [], 16, "", "qun_kinsman_general", "vanguard", 32)
    add("zhang_wu", "张武", "江夏贼", 68, 32, 48, 22, 62, 50, 184, "jiang_xia",
        [], 16, "qun_bandit_raid", "qun_bandit_chief", "vanguard", 28)
    add("chen_sun", "陈孙", "江夏贼", 62, 30, 45, 20, 60, 50, 184, "jiang_xia",
        [], 14, "qun_bandit_raid", "qun_bandit_chief", "vanguard", 25)
    add("kua_neng", "蒯能", "蒯氏族人", 42, 58, 40, 52, 45, 68, 184, "xiang_yang",
        [], 16, "", "qun_minor_scholar", "archer", 42)
    add("liu_xian_lc", "刘璝", "刘璋部将", 58, 48, 55, 42, 55, 75, 194, "cheng_du",
        [], 18, "", "qun_regional_defender", "vanguard", 38)
    add("leng_bao2", "冷苞", "刘璋部将", 55, 38, 50, 32, 52, 72, 194, "zi_tong",
        [], 16, "", "qun_regional_defender", "vanguard", 30)
    add("zhang_yi_lc", "张翼", "刘璋部将", 58, 42, 52, 35, 55, 70, 194, "jian_wei",
        [], 18, "", "qun_regional_defender", "vanguard", 32)
    add("li_hui_lc", "李恢", "刘璋部将", 48, 68, 45, 58, 45, 72, 194, "jian_wei",
        [], 20, "", "qun_scholar_official", "archer", 52)
    add("wang_lei", "王累", "刘璋谏臣", 25, 72, 32, 65, 38, 85, 194, "cheng_du",
        ["frank_remonstrance"], 22, "qun_honest_remonstrance", "qun_loyal_minister", "archer", 65)
    add("huang_quan2", "黄权", "蜀汉名将", 45, 82, 75, 78, 48, 78, 194, "cheng_du",
        [], 28, "qun_strategic_withdraw", "qun_strategic_foresight", "archer", 62)
    add("yan_yan2", "严颜", "巴郡老将", 80, 60, 75, 52, 65, 85, 184, "jiang_zhou",
        [], 28, "qun_veteran_roar", "qun_old_warrior_spirit", "vanguard", 48)
    add("zhao_wei", "赵韪", "刘璋部将", 55, 55, 52, 48, 52, 60, 194, "ba_xi",
        [], 18, "", "qun_rebel_general", "vanguard", 40)

    add("yang_hong", "杨洪", "蜀汉名臣", 25, 72, 48, 75, 42, 82, 200, "wu_yang",
        [], 24, "qun_admin_reform", "qun_capable_minister", "archer", 65)
    add("he_zhi", "何祗", "蜀汉官员", 22, 68, 38, 62, 40, 78, 220, "cheng_du",
        [], 20, "", "qun_minor_official", "archer", 52)
    add("wang_mou", "王谋", "蜀汉官员", 28, 65, 42, 58, 42, 75, 194, "cheng_du",
        [], 18, "", "qun_minor_official", "archer", 50)
    add("yin_guan", "殷观", "蜀汉官员", 30, 62, 40, 55, 42, 72, 194, "cheng_du",
        [], 18, "", "qun_minor_official", "archer", 48)
    add("fei_shi", "费诗", "蜀汉谏臣", 28, 72, 38, 65, 40, 78, 200, "zi_tong",
        ["frank_remonstrance"], 22, "qun_honest_remonstrance", "qun_upright_scholar", "archer", 62)
    add("liu_ba", "刘巴", "蜀汉名臣", 22, 82, 38, 85, 38, 80, 194, "ling_ling",
        ["economic_policy"], 28, "qun_admin_reform", "qun_economic_minister", "archer", 72)

    add("zhao_yun2", "赵累", "关羽部下", 55, 42, 48, 35, 52, 78, 184, "xiang_yang",
        [], 16, "", "qun_loyal_deputy", "vanguard", 38)
    add("mi_fang2", "糜芳", "糜竺之弟", 45, 48, 42, 42, 48, 60, 184, "dong_hai",
        [], 16, "", "qun_turncoat", "vanguard", 35)
    add("fu_shiren", "傅士仁", "蜀汉叛将", 48, 42, 45, 38, 48, 55, 184, "guang_yang",
        [], 16, "", "qun_turncoat", "vanguard", 32)
    add("pan_jun", "潘濬", "东吴名臣", 38, 75, 58, 78, 42, 82, 194, "wu_ling",
        [], 26, "qun_admin_reform", "qun_capable_minister", "archer", 68)
    add("xiang_lang", "向朗", "蜀汉官员", 32, 68, 45, 65, 42, 78, 194, "yi_yang",
        [], 22, "", "qun_minor_official", "archer", 58)
    add("xiang_chong3", "向宠", "蜀汉将领", 62, 58, 65, 48, 55, 85, 207, "yi_yang",
        [], 22, "qun_steady_strike", "qun_reliable_general", "vanguard", 50)

    add("luo_xian2", "罗宪", "蜀汉守将", 58, 62, 62, 55, 52, 78, 260, "yong_an",
        [], 22, "qun_steady_strike", "qun_reliable_general", "vanguard", 52)
    add("huang_chong", "黄崇", "黄权之子", 55, 65, 55, 52, 55, 85, 250, "cheng_du",
        [], 20, "qun_steady_strike", "qun_loyal_son", "vanguard", 50)
    add("li_yi_shu", "李遗", "蜀汉将领", 58, 52, 55, 45, 58, 75, 225, "yun_nan",
        [], 20, "", "qun_reliable_general", "vanguard", 42)
    add("guan_tong", "关统", "关兴之子", 62, 48, 55, 38, 62, 85, 240, "cheng_du",
        [], 20, "", "qun_guan_family", "vanguard", 42)
    add("zhang_zun", "张遵", "张苞之子", 58, 45, 52, 35, 60, 85, 240, "cheng_du",
        [], 18, "", "qun_zhang_family", "vanguard", 38)
    add("li_yi2", "李仪", "蜀汉官员", 25, 62, 35, 55, 38, 72, 220, "cheng_du",
        [], 16, "", "qun_minor_official", "archer", 45)

    # 更多吴国后期人物
    add("lu_ji", "陆绩", "陆逊之子", 25, 80, 32, 62, 38, 78, 200, "wu_county",
        ["scholarly_talent"], 22, "qun_literary_fame", "qun_scholarly_son", "archer", 65)
    add("gu_shao", "顾邵", "顾雍之子", 32, 72, 42, 62, 42, 78, 200, "wu_jun",
        [], 20, "", "qun_scholarly_son", "archer", 58)
    add("gu_cheng", "顾承", "顾雍之孙", 38, 65, 40, 55, 45, 75, 220, "wu_jun",
        [], 18, "", "qun_minor_noble", "archer", 50)
    add("zhu_ji2", "朱纪", "朱据之子", 42, 55, 42, 45, 48, 72, 228, "wu_jun",
        [], 16, "", "qun_minor_noble", "archer", 45)
    add("shi_yi2", "施绩", "东吴将领", 62, 58, 65, 50, 58, 80, 240, "wu_jun",
        [], 22, "qun_steady_strike", "qun_reliable_general", "vanguard", 50)
    add("ding_feng2", "丁奉", "东吴老将", 78, 55, 75, 48, 72, 80, 200, "lu_an",
        [], 26, "qun_fierce_strike", "qun_veteran_hero", "vanguard", 48)
    add("sun_he2", "孙河", "东吴宗室", 62, 55, 58, 48, 58, 78, 184, "fu_chun",
        [], 20, "", "qun_loyal_kinsman", "vanguard", 48)
    add("sun_rong", "孙荣", "东吴宗室", 55, 52, 52, 45, 52, 75, 200, "jian_ye",
        [], 18, "", "qun_minor_noble", "vanguard", 40)
    add("tao_huang", "陶璜", "东吴交州刺史", 48, 72, 62, 68, 45, 80, 240, "jiao_zhi",
        [], 24, "qun_admin_reform", "qun_southern_governor", "archer", 62)
    add("xiang_pu", "项普", "东吴将领", 52, 48, 50, 42, 52, 72, 240, "kuai_ji",
        [], 16, "", "qun_reliable_general", "vanguard", 38)

    # 更多魏国后期人物
    add("wang_ji_wei", "王基", "魏国名将", 65, 72, 78, 62, 58, 85, 228, "dong_lai",
        [], 28, "qun_steady_strike", "qun_reliable_commander", "vanguard", 58)
    add("chen_tai", "陈泰", "魏国名将", 68, 75, 80, 65, 58, 85, 228, "ying_chuan",
        [], 30, "qun_steady_strike", "qun_capable_commander", "vanguard", 62)
    add("deng_ai2", "邓艾", "平蜀功臣", 72, 85, 88, 82, 62, 80, 208, "yi_yang",
        [], 30, "qun_yin_ping_march", "qun_pragmatic_general", "vanguard", 75)
    add("zhong_hui2", "钟会", "谋反叛将", 65, 90, 85, 78, 58, 65, 208, "ying_chuan",
        [], 32, "qun_ambition_strike", "qun_calculating_scholar", "archer", 70)
    add("zhuge_xu", "诸葛绪", "魏国将领", 62, 58, 65, 48, 55, 78, 228, "lang_ya",
        [], 22, "", "qun_reliable_general", "vanguard", 48)
    add("tian_xu", "田续", "魏国将领", 55, 48, 52, 42, 52, 72, 228, "yan_province",
        [], 18, "", "qun_reliable_general", "vanguard", 40)
    add("wei_guan", "卫瓘", "魏国名臣", 42, 78, 58, 75, 48, 80, 240, "he_dong",
        [], 26, "qun_admin_reform", "qun_capable_minister", "archer", 68)
    add("jia_chong", "贾充", "晋国权臣", 48, 68, 62, 72, 48, 70, 240, "ping_yang",
        ["political_manipulation"], 26, "qun_political_scheme", "qun_power_behind_throne", "archer", 48)
    add("xun_kai", "荀顗", "魏国名臣", 28, 75, 45, 78, 42, 82, 240, "ying_chuan",
        [], 24, "qun_admin_reform", "qun_capable_minister", "archer", 68)
    add("pei_xiu", "裴秀", "地图学家", 25, 82, 38, 72, 40, 78, 240, "he_dong",
        ["cartography"], 26, "qun_literary_fame", "qun_cartographer", "archer", 70)
    add("shan_tao2", "山涛", "竹林七贤", 22, 75, 32, 72, 38, 72, 250, "he_nei",
        [], 24, "qun_literary_fame", "qun_seven_sages", "archer", 65)
    add("yang_zhi", "杨芷", "晋国皇后", 18, 72, 15, 55, 32, 75, 260, "hong_nong",
        [], 16, "", "qun_empress", "archer", 72)

    # 补充一些三国初期/中期人物
    add("cheng_pu", "程普", "三朝老将", 82, 58, 78, 52, 68, 92, 184, "you_beiping",
        [], 26, "qun_experienced_strike", "qun_three_dynasty_veteran", "vanguard", 45)
    add("huang_gai2", "黄盖", "三朝老将", 85, 62, 80, 55, 65, 94, 184, "ling_ling",
        [], 28, "qun_sacrifice_strike", "qun_loyal_veteran", "vanguard", 48)
    add("han_dang3", "韩当", "三朝老将", 82, 52, 78, 42, 72, 88, 184, "liao_xi",
        [], 26, "qun_steady_strike", "qun_three_dynasty_veteran", "vanguard", 45)

    add("zhang_cheng", "张承", "张昭之子", 35, 75, 52, 68, 42, 82, 194, "peng_cheng",
        [], 24, "", "qun_scholarly_son", "archer", 62)
    add("zhang_xiu_wu", "张休", "张昭之子", 38, 72, 48, 65, 45, 80, 194, "peng_cheng",
        [], 22, "", "qun_scholarly_son", "archer", 58)
    add("gu_tan", "顾谭", "顾雍之孙", 32, 72, 42, 62, 42, 72, 220, "wu_jun",
        [], 20, "", "qun_minor_scholar", "archer", 52)
    add("gu_cheng2", "顾承", "顾雍之孙", 38, 65, 40, 55, 45, 72, 220, "wu_jun",
        [], 18, "", "qun_minor_noble", "vanguard", 48)

    add("zhu_yi_wu", "朱异", "东吴将领", 72, 55, 68, 45, 65, 75, 228, "wu_jun",
        [], 22, "qun_steady_strike", "qun_reliable_general", "vanguard", 45)
    add("shi_ji", "施绩", "东吴将领", 62, 58, 65, 50, 58, 80, 240, "wu_jun",
        [], 22, "qun_steady_strike", "qun_reliable_general", "vanguard", 50)

    add("sun_he_wu", "孙桓", "东吴宗室", 68, 58, 65, 48, 62, 82, 200, "fu_chun",
        [], 22, "qun_steady_strike", "qun_reliable_kinsman", "vanguard", 50)
    add("sun_shao2", "孙韶", "东吴镇守", 65, 58, 68, 52, 60, 82, 200, "wu_jun",
        [], 24, "qun_steady_strike", "qun_reliable_kinsman", "vanguard", 50)
    add("sun_yi_wu", "孙异", "东吴将领", 58, 52, 55, 45, 55, 78, 228, "jian_ye",
        [], 18, "", "qun_reliable_general", "vanguard", 42)

    add("lu_xun2", "陆逊", "社稷之臣", 62, 92, 88, 85, 65, 88, 200, "wu_county",
        [], 34, "qun_flame_command", "qun_scholarly_general", "archer", 78)
    add("lu_kang2", "陆抗", "东吴名将", 68, 80, 85, 72, 60, 88, 240, "jian_ye",
        [], 32, "qun_steady_strike", "qun_last_pillar", "archer", 75)
    add("lu_yan2", "陆晏", "陆抗之子", 55, 52, 52, 42, 55, 80, 260, "jian_ye",
        [], 20, "qun_steady_strike", "qun_loyal_son", "vanguard", 45)

    # ================================================================
    # 第九类：更多黄巾/盗贼/山贼
    # ================================================================
    add("du_yuan", "杜远", "山贼", 58, 28, 42, 18, 58, 48, 184, "dong_hai",
        [], 14, "qun_bandit_raid", "qun_mountain_bandit", "vanguard", 22)
    add("liao_hua_hj", "廖化", "黄巾余党", 72, 58, 68, 48, 65, 88, 184, "xiang_yang",
        [], 22, "qun_pioneer_strike", "qun_enduring_pioneer", "vanguard", 42)
    add("zhou_cang2", "周仓", "关羽护卫", 80, 30, 52, 22, 72, 98, 184, "xiang_yang",
        [], 18, "qun_loyal_blade_strike", "qun_loyal_guardian", "vanguard", 25)
    add("pei_yuan_shao", "裴元绍", "黄巾余党", 55, 28, 42, 20, 55, 58, 184, "ru_nan",
        [], 15, "qun_horse_thief", "qun_ambush_bandit", "vanguard", 20)
    add("wang_zhi", "王植", "荥阳太守", 48, 42, 45, 38, 48, 62, 184, "xing_yang",
        [], 16, "", "qun_minor_governor", "archer", 35)
    add("bian_xi", "卞喜", "汜水关守将", 58, 38, 48, 32, 55, 58, 184, "si_shui",
        [], 16, "", "qun_pass_guard", "vanguard", 32)
    add("kong_xiu", "孔秀", "东岭关守将", 52, 35, 45, 30, 52, 58, 184, "dong_ling",
        [], 14, "", "qun_pass_guard", "vanguard", 28)
    add("han_fu_gk", "韩福", "洛阳太守", 48, 40, 42, 35, 48, 60, 184, "luo_yang",
        [], 14, "", "qun_pass_guard", "vanguard", 30)
    add("meng_tan", "孟坦", "洛阳牙将", 58, 32, 48, 25, 58, 55, 184, "luo_yang",
        [], 14, "", "qun_pass_guard", "vanguard", 28)

    add("hu_ban", "胡班", "荥阳从事", 42, 48, 40, 42, 45, 72, 184, "xing_yang",
        [], 14, "", "qun_minor_official", "vanguard", 38)
    add("qin_qi", "秦琪", "黄河渡口守将", 55, 32, 45, 25, 55, 55, 184, "hua_zhou",
        [], 14, "", "qun_pass_guard", "vanguard", 28)
    add("cai_yang", "蔡阳", "汝南太守", 68, 48, 58, 38, 60, 65, 184, "ru_nan",
        [], 20, "", "qun_reliable_general", "vanguard", 38)

    # ================================================================
    # 第十类：女将 / 特殊NPC
    # ================================================================
    add("zhu_rong", "祝融夫人", "南蛮女将", 72, 48, 58, 32, 72, 78, 225, "yun_nan",
        ["flying_knives"], 24, "qun_flying_knives", "qun_nan_heroine", "vanguard", 48)
    add("sun_shangxiang", "孙尚香", "弓腰姬", 78, 65, 62, 48, 80, 75, 200, "jian_ye",
        ["archer_princess"], 28, "qun_archer_princess", "qun_warrior_princess", "archer", 55)
    add("da_qiao", "大乔", "江东二乔", 18, 68, 15, 42, 35, 82, 194, "wan_cheng",
        ["beauty"], 18, "qun_charm", "qun_peerless_beauty", "archer", 90)
    add("xiao_qiao", "小乔", "江东二乔", 18, 65, 12, 38, 38, 80, 194, "wan_cheng",
        ["beauty"], 18, "qun_charm", "qun_peerless_beauty", "archer", 92)
    add("zhen_ji", "甄姬", "文昭皇后", 18, 72, 15, 55, 38, 78, 184, "zhong_shan",
        ["graceful_elegance"], 20, "qun_graceful_elegance", "qun_tragic_beauty", "archer", 88)
    add("cai_yan", "蔡琰", "才女", 18, 85, 12, 48, 35, 70, 184, "chen_liu",
        ["literary_composition"], 24, "qun_literary_fame", "qun_talented_woman", "archer", 82)
    add("luo_shi", "骆氏", "孟获妻", 22, 58, 18, 42, 35, 72, 225, "yun_nan",
        [], 16, "", "qun_nan_noblewoman", "archer", 55)
    add("yue_ying", "黄月英", "诸葛之妻", 22, 82, 18, 58, 38, 80, 207, "xiang_yang",
        ["mechanical_talent", "invention"], 24, "qun_invention", "qun_wise_woman", "archer", 75)

    # ================================================================
    # 第十一类：更多三国初期人物
    # ================================================================
    add("zou_jing", "邹靖", "幽州校尉", 58, 52, 58, 45, 55, 75, 184, "you_zhou",
        [], 18, "", "qun_regional_commander", "vanguard", 42)
    add("liu_yan2", "刘焉", "益州牧", 55, 68, 65, 75, 48, 80, 184, "cheng_du",
        [], 26, "qun_provincial_aura", "qun_yi_governor", "archer", 72)
    add("liu_yan_yu", "刘虞", "幽州牧", 30, 65, 55, 82, 42, 90, 184, "you_zhou",
        [], 26, "qun_benevolent_rule_north", "qun_peaceful_governance", "archer", 80)
    add("gong_sun_zan", "公孙瓒", "白马将军", 82, 58, 78, 52, 85, 80, 184, "you_zhou",
        [], 28, "qun_white_horse_charge", "qun_white_horse_general", "cavalry", 48)

    add("yuan_yi", "袁遗", "山阳太守", 38, 52, 45, 48, 42, 65, 184, "shan_yang",
        [], 16, "", "qun_minor_governor", "archer", 40)
    add("zhang_zi2", "张咨", "南阳太守", 35, 48, 42, 45, 42, 65, 184, "wan_cheng",
        [], 14, "", "qun_minor_governor", "archer", 38)

    add("zhu_zhi_wu", "朱治", "东吴元老", 65, 62, 72, 58, 58, 90, 184, "yang_di",
        [], 24, "qun_steady_strike", "qun_veteran_minister", "vanguard", 58)

    # 更多武将 - 填补空缺
    for i in range(20):
        names = [("zhao_lei", "赵累"), ("sun_jing2", "孙敬"), ("li_hui2", "李辉"),
                 ("zhou_ping", "周平"), ("wu_yi2", "武艺"), ("feng_yuan", "冯远"),
                 ("ma_zhong2", "马忠"), ("gao_shun2", "高顺"), ("chen_wu2", "陈武"),
                 ("yang_hong2", "杨洪"), ("zhou_qi", "周起"), ("hu_fang", "胡方"),
                 ("tian_hong", "田洪"), ("shi_le", "石磊"), ("qian_tong", "钱通"),
                 ("he_lin", "何琳"), ("sun_ben2", "孙贲"), ("zhou_yu2", "周渔"),
                 ("liang_gang", "梁刚"), ("sun_yi3", "孙仪")]
        if i < len(names):
            pid, pname = names[i]
            add(pid, pname, "武将", 60 + i % 15, 40 + i % 20, 55 + i % 15,
                35 + i % 20, 55 + i % 15, 65 + i % 15, 184 + (i % 4) * 10,
                cities[i % len(cities)],
                [], 16 + i % 8, "qun_steady_strike", "qun_reliable_general",
                ["vanguard", "cavalry", "archer"][i % 3], 30 + i % 15)

    # 更多文臣
    for i in range(25):
        names = [("song_zhong", "宋忠"), ("pan_mi", "潘秘"), ("pan_zhang2", "潘璋"),
                 ("deng_zhi2", "邓芝"), ("cheng_ji", "程畿"), ("yang_xi", "杨戏"),
                 ("zhong_yuan", "钟援"), ("liang_feng", "梁丰"), ("zhang_biao2", "张表"),
                 ("zhang_yi2", "张翼"), ("li_zhao2", "李昭"), ("wang_yuan", "王元"),
                 ("zhou_jing", "周景"), ("guo_wen", "郭文"), ("zhao_fan", "赵范"),
                 ("liu_yin2", "刘胤"), ("zhang_hu2", "张虎"), ("sun_huan2", "孙桓"),
                 ("lu_yu2", "陆郁"), ("zhang_chang", "张敞"), ("zhou_xun", "周循"),
                 ("zhu_zhu", "朱柱"), ("sun_lu", "孙虑"), ("sun_bian", "孙辨"),
                 ("zhuo_ying", "卓膺")]
        if i < len(names):
            pid, pname = names[i]
            add(pid, pname, "文臣", 22 + i % 15, 60 + i % 25, 35 + i % 15,
                65 + i % 20, 38 + i % 12, 70 + i % 15, 190 + (i % 4) * 10,
                cities[i % len(cities)],
                [], 18 + i % 6, "qun_admin_reform", "qun_scholar_official",
                "archer", 50 + i % 20)

    return new_generals


# ============================================================
# 3. 主程序
# ============================================================
def main():
    print("=" * 60)
    print("三国志11武将补充脚本")
    print("=" * 60)

    # 读取已有武将
    existing_ids = load_existing_ids()
    existing_generals = load_existing_generals()

    print(f"\n已有武将数量: {len(existing_generals)}")
    print(f"已有武将ID数: {len(existing_ids)}")

    # 生成新武将
    new_generals = generate_new_generals(existing_ids)

    print(f"生成新武将数量: {len(new_generals)}")
    print(f"追加后总武将数量: {len(existing_generals) + len(new_generals)}")

    if len(new_generals) == 0:
        print("\n没有新武将会被添加（所有ID都已存在）。")
        return

    # 追加到generals.json
    all_generals = existing_generals + new_generals
    with open(GENERALS_FILE, "w", encoding="utf-8") as f:
        json.dump(all_generals, f, ensure_ascii=False, indent=2)

    print(f"\n已成功写入 {GENERALS_FILE}")

    # 统计信息
    print("\n" + "=" * 60)
    print("统计信息")
    print("=" * 60)

    # 按类型统计（根据属性分布）
    warriors = 0
    scholars = 0
    balanced = 0
    leaders = 0
    special = 0

    for g in new_generals:
        avg_stats = (g["strength"] + g["intelligence"] + g["command"] + g["politics"] + g["speed"]) / 5
        if g["strength"] >= 80:
            warriors += 1
        elif g["intelligence"] >= 75:
            scholars += 1
        elif g["command"] >= 75 and g["strength"] >= 70:
            leaders += 1
        elif g["specialSkills"] and any("magic" in s or "heal" in s for s in g["specialSkills"]):
            special += 1
        else:
            balanced += 1

    print(f"\n新武将类型分布:")
    print(f"  猛将型 (武力>=80): {warriors}")
    print(f"  谋士型 (智力>=75): {scholars}")
    print(f"  统帅型 (统率>=75+武力>=70): {leaders}")
    print(f"  特殊型 (特殊技能): {special}")
    print(f"  均衡型 (其他): {balanced}")

    # 按登场年份统计
    year_counts = {}
    for g in new_generals:
        year = g["appearYear"]
        year_counts[year] = year_counts.get(year, 0) + 1

    print(f"\n按登场年份分布:")
    for year in sorted(year_counts.keys()):
        print(f"  {year}年: {year_counts[year]}人")

    # 按兵种统计
    formation_counts = {}
    for g in new_generals:
        f = g["preferredFormation"]
        formation_counts[f] = formation_counts.get(f, 0) + 1

    print(f"\n按兵种分布:")
    for f, count in sorted(formation_counts.items(), key=lambda x: -x[1]):
        print(f"  {f}: {count}人")

    # 属性范围
    strengths = [g["strength"] for g in new_generals]
    intelligences = [g["intelligence"] for g in new_generals]
    commands = [g["command"] for g in new_generals]
    politics = [g["politics"] for g in new_generals]
    speeds = [g["speed"] for g in new_generals]

    print(f"\n新武将属性范围:")
    print(f"  武力: {min(strengths)} - {max(strengths)}")
    print(f"  智力: {min(intelligences)} - {max(intelligences)}")
    print(f"  统率: {min(commands)} - {max(commands)}")
    print(f"  政治: {min(politics)} - {max(politics)}")
    print(f"  速度: {min(speeds)} - {max(speeds)}")

    # 列出前10个新武将
    print(f"\n新增武将示例（前10个）:")
    for i, g in enumerate(new_generals[:10]):
        print(f"  {i+1}. {g['name']} ({g['id']}) - {g['title']} - 武{g['strength']} 智{g['intelligence']} 统{g['command']} 政{g['politics']}")

    print(f"\n总计新增: {len(new_generals)} 人")
    print(f"最终总数: {len(all_generals)} 人")
    print("=" * 60)


if __name__ == "__main__":
    main()
