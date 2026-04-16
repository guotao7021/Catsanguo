#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
城市武将分配平衡脚本

为每个剧本中武将不足的城市补充武将。
目标武将数量:
  - 大城(huge/large): 5人
  - 中城(medium): 4人
  - 小城(small): 3人

补充规则:
  - 武将在该剧本的年份必须已经出现 (appearYear <= scenario_year)
  - 优先补充尚未分配到任何城市的武将
  - 保持武将属于同一势力优先
  - 武将ID必须存在于generals.json中
"""

import json
import os
import shutil
from datetime import datetime

# 文件路径
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
SCENARIOS_PATH = os.path.join(SCRIPT_DIR, "scenarios.json")
GENERALS_PATH = os.path.join(SCRIPT_DIR, "generals.json")
CITIES_PATH = os.path.join(SCRIPT_DIR, "cities.json")

# 目标武将数量
TARGET_GENERALS = {
    "huge": 5,
    "large": 5,
    "medium": 4,
    "small": 3,
}

# 势力-城市映射(用于同势力优先)
FACTION_CITY_PREFERENCE = {
    "liubei": "xu_zhou",
    "guanyu": "xu_zhou",
    "zhangfei": "xu_zhou",
    "caocao": "chen_liu",
    "sunquan": "jian_ye",
    "sunce": "wu_jun",
    "machao": "tian_shui",
    "ma_teng": "tian_shui",
    "yuanshao": "ye_cheng",
    "liubiao": "xiang_yang",
    "gongsunzan": "you_zhou",
    "liuyan": "cheng_du",
    "liuzhang": "cheng_du",
    "zhanglu": "han_zhong",
    "yuan_shu": "shou_chun",
    "lvbu": "xia_pi",
    "dongzhuo": "chang_an",
}


def load_json(path, encoding="utf-8-sig"):
    """加载JSON文件"""
    with open(path, "r", encoding=encoding) as f:
        return json.load(f)


def save_json(path, data, encoding="utf-8-sig"):
    """保存JSON文件，保持格式"""
    with open(path, "w", encoding=encoding) as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    print(f"  已保存: {path}")


def backup_file(path):
    """备份文件"""
    backup_path = path + ".bak_" + datetime.now().strftime("%Y%m%d_%H%M%S")
    shutil.copy2(path, backup_path)
    print(f"  已备份: {backup_path}")
    return backup_path


def get_city_scale(city_id, cities_data):
    """获取城市规模"""
    for city in cities_data:
        if city["id"] == city_id:
            return city.get("cityScale", "small")
    return "small"


def get_target_count(city_id, cities_data):
    """根据城市规模获取目标武将数量"""
    scale = get_city_scale(city_id, cities_data)
    return TARGET_GENERALS.get(scale, 3)


def get_all_assigned_generals(factions):
    """获取该剧本中所有已分配武将的ID集合"""
    assigned = set()
    for faction in factions:
        for gen in faction.get("initialGenerals", []):
            assigned.add(gen["generalId"])
    return assigned


def get_city_generals(faction, city_id):
    """获取某势力某城市已有的武将列表"""
    return [
        gen for gen in faction.get("initialGenerals", []) if gen.get("assignedCityId") == city_id
    ]


def get_available_generals(scenario_year, all_generals, assigned_generals):
    """获取可用的未分配武将列表 (appearYear <= scenario_year)"""
    available = []
    for gen in all_generals:
        if gen["id"] not in assigned_generals and gen.get("appearYear", 999) <= scenario_year:
            available.append(gen)
    return available


def get_faction_leader_id(faction):
    """获取势力的主公ID"""
    return faction.get("leaderId", "")


def get_faction_theme_generals(available, city_id, faction_leader_id, scenario_year):
    """
    获取适合该城市/势力的武将列表，按优先级排序:
    1. 与势力同主题的武将 (通过FACTION_CITY_PREFERENCE映射)
    2. 其他可用武将
    """
    # 找出该城市对应的势力主题武将
    theme_generals = []
    other_generals = []

    city_theme_leader = None
    # 根据城市ID反查可能的势力主公
    for leader_id, pref_city in FACTION_CITY_PREFERENCE.items():
        if pref_city == city_id:
            theme_generals_candidate = [
                g for g in available if g.get("appearCityId") == city_id
            ]
            if theme_generals_candidate:
                theme_generals.extend(theme_generals_candidate)

    # 如果城市有明确的势力关联，优先该势力相关的武将
    # 通过检查城市是否是某个势力的initialCityId
    for gen in available:
        if gen.get("appearCityId") == city_id:
            if gen not in theme_generals:
                theme_generals.append(gen)

    for gen in available:
        if gen not in theme_generals:
            other_generals.append(gen)

    # 合并，主题武将优先
    return theme_generals + other_generals


def balance_scenario(scenario, all_generals, cities_data):
    """平衡一个剧本的城市武将分配"""
    scenario_id = scenario["id"]
    scenario_name = scenario["name"]
    scenario_year = scenario["startDate"]["year"]

    print(f"\n{'='*60}")
    print(f"剧本: {scenario_name} ({scenario_id}) - 年份: {scenario_year}")
    print(f"{'='*60}")

    factions = scenario.get("factions", [])
    stats = []

    # 获取该剧本所有已分配的武将ID
    all_assigned = get_all_assigned_generals(factions)
    print(f"  已分配武将总数: {len(all_assigned)}")

    for faction in factions:
        faction_id = faction.get("factionId", "")
        faction_name = faction.get("factionName", "")
        city_ids = faction.get("initialCityIds", [])

        print(f"\n  势力: {faction_name} ({faction_id})")
        print(f"  城市: {', '.join(city_ids)}")

        for city_id in city_ids:
            current_generals = get_city_generals(faction, city_id)
            current_count = len(current_generals)
            target_count = get_target_count(city_id, cities_data)
            city_scale = get_city_scale(city_id, cities_data)

            deficit = target_count - current_count

            if deficit > 0:
                print(f"    城市: {city_id} (规模: {city_scale}) - "
                      f"当前: {current_count}人, 目标: {target_count}人, "
                      f"缺额: {deficit}人")

                # 重新计算已分配武将
                all_assigned_in_scenario = get_all_assigned_generals(factions)
                available = get_available_generals(
                    scenario_year, all_generals, all_assigned_in_scenario
                )

                # 按主题优先排序
                available_sorted = get_faction_theme_generals(
                    available, city_id, get_faction_leader_id(faction), scenario_year
                )

                # 补充武将
                added = 0
                for gen in available_sorted:
                    if added >= deficit:
                        break
                    if gen["id"] in all_assigned_in_scenario:
                        continue

                    # 添加武将到该城市
                    new_general = {
                        "generalId": gen["id"],
                        "assignedCityId": city_id,
                        "initialLevel": max(3, current_count > 0 and
                                          min(g.get("initialLevel", 5) for g in current_generals) - 1
                                          or 5),
                        "initialLoyalty": 80,
                    }
                    faction["initialGenerals"].append(new_general)
                    all_assigned_in_scenario.add(gen["id"])
                    all_assigned.add(gen["id"])

                    gen_name = gen.get("name", gen["id"])
                    print(f"      补充: {gen_name} (出现年份: {gen.get('appearYear', '?')})")
                    added += 1

                if added > 0:
                    stats.append({
                        "scenario": scenario_name,
                        "faction": faction_name,
                        "city": city_id,
                        "scale": city_scale,
                        "before": current_count,
                        "target": target_count,
                        "added": added,
                        "after": current_count + added,
                    })
            else:
                print(f"    城市: {city_id} (规模: {city_scale}) - "
                      f"当前: {current_count}人, 目标: {target_count}人 [充足]")

    return stats


def main():
    print("城市武将分配平衡脚本")
    print("="*60)

    # 加载数据
    print("\n正在加载数据...")
    scenarios = load_json(SCENARIOS_PATH)
    all_generals = load_json(GENERALS_PATH)
    cities = load_json(CITIES_PATH)

    print(f"  剧本数: {len(scenarios)}")
    print(f"  武将数: {len(all_generals)}")
    print(f"  城市数: {len(cities)}")

    # 创建将军ID到将军对象的映射
    general_map = {g["id"]: g for g in all_generals}

    # 备份原文件
    print("\n正在备份原文件...")
    backup_file(SCENARIOS_PATH)

    # 处理每个剧本
    all_stats = []
    for scenario in scenarios:
        stats = balance_scenario(scenario, all_generals, cities)
        all_stats.extend(stats)

    # 保存修改后的文件
    print(f"\n{'='*60}")
    print("正在保存修改后的文件...")
    save_json(SCENARIOS_PATH, scenarios)

    # 输出统计
    print(f"\n{'='*60}")
    print("补充结果统计")
    print(f"{'='*60}")

    if not all_stats:
        print("\n所有城市武将数量均已达标，无需补充！")
    else:
        print(f"\n共补充 {sum(s['added'] for s in all_stats)} 名武将到 {len(all_stats)} 个城市:\n")

        print(f"{'剧本':<15} {'势力':<12} {'城市':<12} {'规模':<6} "
              f"{'补充前':<6} {'目标':<6} {'补充':<6} {'补充后':<6}")
        print("-" * 80)

        for s in all_stats:
            print(f"{s['scenario']:<15} {s['faction']:<12} {s['city']:<12} {s['scale']:<6} "
                  f"{s['before']:<6} {s['target']:<6} {s['added']:<6} {s['after']:<6}")

        # 按剧本汇总
        print(f"\n{'='*60}")
        print("按剧本汇总:")
        scenario_summary = {}
        for s in all_stats:
            if s["scenario"] not in scenario_summary:
                scenario_summary[s["scenario"]] = {"cities": 0, "generals": 0}
            scenario_summary[s["scenario"]]["cities"] += 1
            scenario_summary[s["scenario"]]["generals"] += s["added"]

        for scenario_name, summary in scenario_summary.items():
            print(f"  {scenario_name}: 补充 {summary['generals']} 名武将到 {summary['cities']} 个城市")

    print(f"\n{'='*60}")
    print("处理完成！")


if __name__ == "__main__":
    main()
