using System;
using System.Collections.Generic;
using System.Linq;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;
using static CatSanguo.Data.Schemas.DiplomacyRelation;

namespace CatSanguo.Core;

/// <summary>
/// 外交关系管理器
/// 管理势力间的外交关系（敌对/中立/贸易/同盟/停战）
/// </summary>
public class DiplomacyManager
{
    private readonly EventBus _eventBus;

    /// <summary>
    /// 外交关系字典：key = "factionAId_factionBId"（按字母序排序）
    /// </summary>
    private readonly Dictionary<string, DiplomacyRelationData> _relations = new();

    public DiplomacyManager(EventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// 获取两个势力之间的关系
    /// </summary>
    public Data.Schemas.DiplomacyRelation GetRelation(string factionAId, string factionBId)
    {
        string key = MakeKey(factionAId, factionBId);
        if (_relations.TryGetValue(key, out var data))
        {
            // 检查同盟/停战是否过期
            if (data.Relation == Data.Schemas.DiplomacyRelation.Alliance || data.Relation == Data.Schemas.DiplomacyRelation.Ceasefire)
            {
                var gs = GameState.Instance;
                if (gs.CurrentDate.Year > data.ExpireYear ||
                    (gs.CurrentDate.Year == data.ExpireYear && gs.CurrentDate.Month > data.ExpireMonth))
                {
                    data.Relation = Data.Schemas.DiplomacyRelation.Neutral;
                }
            }
            return data.Relation;
        }
        return Data.Schemas.DiplomacyRelation.Hostile; // 默认敌对
    }

    /// <summary>
    /// 设置两个势力之间的关系
    /// </summary>
    public void SetRelation(string factionAId, string factionBId, Data.Schemas.DiplomacyRelation relation, int durationMonths = 0)
    {
        string key = MakeKey(factionAId, factionBId);

        var gs = GameState.Instance;
        int expireYear = gs.CurrentDate.Year;
        int expireMonth = gs.CurrentDate.Month;

        if (durationMonths > 0)
        {
            int totalMonths = gs.CurrentDate.Month + durationMonths;
            expireYear += totalMonths / 12;
            expireMonth = totalMonths % 12;
            if (expireMonth == 0) expireMonth = 12;
        }

        _relations[key] = new DiplomacyRelationData
        {
            FactionAId = factionAId,
            FactionBId = factionBId,
            Relation = relation,
            ExpireYear = expireYear,
            ExpireMonth = expireMonth
        };
    }

    /// <summary>
    /// 提议同盟
    /// </summary>
    public bool ProposeAlliance(string myFactionId, string targetFactionId, int durationMonths = 12)
    {
        var currentRelation = GetRelation(myFactionId, targetFactionId);

        // 不能与敌对势力直接同盟
        if (currentRelation == DiplomacyRelation.Hostile)
            return false;

        // 已经是同盟
        if (currentRelation == DiplomacyRelation.Alliance)
            return false;

        SetRelation(myFactionId, targetFactionId, DiplomacyRelation.Alliance, durationMonths);
        return true;
    }

    /// <summary>
    /// 提议停战
    /// </summary>
    public bool ProposeCeasefire(string myFactionId, string targetFactionId, int durationMonths = 6)
    {
        SetRelation(myFactionId, targetFactionId, DiplomacyRelation.Ceasefire, durationMonths);
        return true;
    }

    /// <summary>
    /// 提议贸易关系
    /// </summary>
    public bool ProposeTrade(string myFactionId, string targetFactionId)
    {
        var currentRelation = GetRelation(myFactionId, targetFactionId);

        // 敌对势力不能贸易
        if (currentRelation == DiplomacyRelation.Hostile)
            return false;

        SetRelation(myFactionId, targetFactionId, DiplomacyRelation.Trade);
        return true;
    }

    /// <summary>
    /// 宣战（设置为敌对）
    /// </summary>
    public void DeclareWar(string myFactionId, string targetFactionId)
    {
        // 清除所有现有关系
        string key = MakeKey(myFactionId, targetFactionId);
        _relations.Remove(key);

        // 设置为敌对（不存储，因为敌对是默认值）
    }

    /// <summary>
    /// 检查是否可以攻击（非同盟关系）
    /// </summary>
    public bool CanAttack(string attackerFactionId, string targetFactionId)
    {
        var relation = GetRelation(attackerFactionId, targetFactionId);
        return relation != DiplomacyRelation.Alliance;
    }

    /// <summary>
    /// 获取指定势力的所有外交关系
    /// </summary>
    public List<DiplomacyRelationData> GetFactionRelations(string factionId)
    {
        return _relations.Values
            .Where(r => r.FactionAId == factionId || r.FactionBId == factionId)
            .ToList();
    }

    /// <summary>
    /// 生成关系字典的 key（按字母序排序确保一致性）
    /// </summary>
    private static string MakeKey(string factionAId, string factionBId)
    {
        if (string.Compare(factionAId, factionBId, StringComparison.Ordinal) < 0)
            return $"{factionAId}_{factionBId}";
        return $"{factionBId}_{factionAId}";
    }
}

/// <summary>
/// 外交关系数据
/// </summary>
public class DiplomacyRelationData
{
    public string FactionAId { get; set; } = "";
    public string FactionBId { get; set; } = "";
    public Data.Schemas.DiplomacyRelation Relation { get; set; } = Data.Schemas.DiplomacyRelation.Hostile;
    public int ExpireYear { get; set; }
    public int ExpireMonth { get; set; }

    /// <summary>
    /// 获取对方势力 ID
    /// </summary>
    public string GetOpponentFactionId(string myFactionId)
    {
        return FactionAId == myFactionId ? FactionBId : FactionAId;
    }
}
