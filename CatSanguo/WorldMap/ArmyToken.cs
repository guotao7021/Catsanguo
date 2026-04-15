using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Data.Schemas;

namespace CatSanguo.WorldMap;

public class ArmyToken
{
    public string Id { get; set; } = "";
    public List<string> GeneralIds { get; set; } = new();
    public List<GeneralDeployEntry> GeneralEntries { get; set; } = new();
    public string LeadGeneralName { get; set; } = "";
    public string Team { get; set; } = "player";
    public string LeadFormation { get; set; } = "vanguard";

    // Position state
    public string? CurrentCityId { get; set; }
    public string? TargetCityId { get; set; }
    public List<string>? MovePath { get; set; }
    public int CurrentSegmentIndex { get; set; }
    public float SegmentProgress { get; set; }
    public bool IsMoving => MovePath != null && MovePath.Count > 1 && CurrentSegmentIndex < MovePath.Count - 1;

    // Rendering
    public Vector2 ScreenPosition { get; set; }
    public bool IsSelected { get; set; }

    // Movement trail
    private readonly Vector2[] _trailPositions = new Vector2[4];
    private int _trailIndex = 0;
    private float _trailTimer = 0;

    private const float SegmentDuration = 2.0f;

    // ==================== 武将配置管理 ====================

    public void SetGeneralDeployConfig(GeneralDeployEntry entry)
    {
        // 移除旧配置（如果有）
        GeneralEntries.RemoveAll(e => e.GeneralId == entry.GeneralId);
        GeneralEntries.Add(entry);
        
        // 同步更新 GeneralIds（向后兼容）
        if (!GeneralIds.Contains(entry.GeneralId))
        {
            GeneralIds.Add(entry.GeneralId);
        }
    }

    public GeneralDeployEntry? GetDeployConfig(string generalId)
    {
        return GeneralEntries.FirstOrDefault(e => e.GeneralId == generalId);
    }

    public void RemoveGeneralDeployConfig(string generalId)
    {
        GeneralEntries.RemoveAll(e => e.GeneralId == generalId);
        GeneralIds.Remove(generalId);
    }

    public void SyncGeneralIds()
    {
        GeneralIds = GeneralEntries.Select(e => e.GeneralId).ToList();
    }

    public void StartMove(List<string> path)
    {
        if (path.Count < 2) return;
        MovePath = path;
        CurrentSegmentIndex = 0;
        SegmentProgress = 0;
        CurrentCityId = null;
        TargetCityId = path[^1];
        // Reset trail
        for (int i = 0; i < _trailPositions.Length; i++)
            _trailPositions[i] = ScreenPosition;
    }

    public void StopAtCity(string cityId)
    {
        CurrentCityId = cityId;
        TargetCityId = null;
        MovePath = null;
        CurrentSegmentIndex = 0;
        SegmentProgress = 0;
    }

    public string? Update(float dt, Dictionary<string, CityNode> cityLookup)
    {
        if (!IsMoving || MovePath == null) return null;

        // Update trail
        _trailTimer += dt;
        if (_trailTimer >= 0.08f)
        {
            _trailTimer = 0;
            _trailPositions[_trailIndex % _trailPositions.Length] = ScreenPosition;
            _trailIndex++;
        }

        SegmentProgress += dt / SegmentDuration;

        if (SegmentProgress >= 1f)
        {
            CurrentSegmentIndex++;
            SegmentProgress = 0;

            if (CurrentSegmentIndex >= MovePath.Count - 1)
            {
                string arrivedCity = MovePath[^1];
                StopAtCity(arrivedCity);
                return arrivedCity;
            }
            else
            {
                string intermediateCity = MovePath[CurrentSegmentIndex];
                return intermediateCity;
            }
        }

        // Interpolate screen position
        if (MovePath.Count > CurrentSegmentIndex + 1)
        {
            string fromId = MovePath[CurrentSegmentIndex];
            string toId = MovePath[CurrentSegmentIndex + 1];

            if (cityLookup.TryGetValue(fromId, out var fromNode) &&
                cityLookup.TryGetValue(toId, out var toNode))
            {
                float eased = EaseInOutCubic(SegmentProgress);
                ScreenPosition = Vector2.Lerp(fromNode.Center, toNode.Center, eased);
            }
        }

        return null;
    }

    public void UpdateStationaryPosition(Dictionary<string, CityNode> cityLookup)
    {
        if (CurrentCityId != null && cityLookup.TryGetValue(CurrentCityId, out var city))
        {
            ScreenPosition = city.Center + new Vector2(0, -30);
        }
    }

    public string? GetNearestCityId()
    {
        if (CurrentCityId != null) return CurrentCityId;
        if (MovePath != null && CurrentSegmentIndex < MovePath.Count)
            return MovePath[CurrentSegmentIndex];
        return null;
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, float battleTime)
    {
        Vector2 pos = ScreenPosition;
        Color baseColor = Team == "player" ? new Color(50, 110, 210) : new Color(210, 50, 50);
        Color flagColor = Team == "player" ? new Color(70, 140, 240) : new Color(240, 70, 70);

        // Movement trail (ghost copies)
        if (IsMoving)
        {
            for (int i = 0; i < _trailPositions.Length; i++)
            {
                int idx = (_trailIndex - 1 - i + _trailPositions.Length * 10) % _trailPositions.Length;
                Vector2 tp = _trailPositions[idx];
                float alpha = (1f - (float)(i + 1) / (_trailPositions.Length + 1)) * 0.25f;
                sb.Draw(pixel, new Rectangle((int)tp.X - 4, (int)tp.Y - 4, 8, 8), baseColor * alpha);
            }
        }

        // Selection pulsing glow
        if (IsSelected)
        {
            float pulse = 0.6f + 0.4f * MathF.Sin(battleTime * 5f);
            Color glowColor = new Color(255, 220, 100) * (0.35f * pulse);
            sb.Draw(pixel, new Rectangle((int)pos.X - 16, (int)pos.Y - 24, 32, 40), glowColor);
        }

        // Banner pole
        int poleX = (int)pos.X;
        int poleBottom = (int)pos.Y;
        int poleTop = poleBottom - 22;
        sb.Draw(pixel, new Rectangle(poleX, poleTop, 2, 22), new Color(90, 75, 55));

        // Banner flag
        int flagW = 18;
        int flagH = 12;
        sb.Draw(pixel, new Rectangle(poleX + 2, poleTop, flagW, flagH), flagColor);
        // Flag border highlight
        sb.Draw(pixel, new Rectangle(poleX + 2, poleTop, flagW, 1), Color.White * 0.3f);
        // Flag bottom notch (triangular cut - approximate with darker bottom-right)
        sb.Draw(pixel, new Rectangle(poleX + 2 + flagW - 4, poleTop + flagH - 3, 4, 3), flagColor * 0.5f);

        // Formation icon on flag
        DrawFormationIcon(sb, pixel, poleX + 2 + flagW / 2, poleTop + flagH / 2, LeadFormation);

        // Base marker (small colored rectangle at position)
        sb.Draw(pixel, new Rectangle((int)pos.X - 5, (int)pos.Y - 3, 10, 6), baseColor);
        sb.Draw(pixel, new Rectangle((int)pos.X - 5, (int)pos.Y - 3, 10, 1), Color.White * 0.2f);

        // General count badge
        if (GeneralIds.Count > 1)
        {
            sb.Draw(pixel, new Rectangle((int)pos.X + 8, poleTop - 2, 12, 12), new Color(40, 35, 28, 200));
            sb.DrawString(font, $"{GeneralIds.Count}", new Vector2(pos.X + 9, poleTop - 2), Color.White);
        }

        // Name label
        var nameSize = font.MeasureString(LeadGeneralName);
        Vector2 namePos = new Vector2(pos.X - nameSize.X / 2, pos.Y + 5);
        sb.DrawString(font, LeadGeneralName, namePos + new Vector2(1, 1), new Color(20, 18, 12, 180));
        sb.DrawString(font, LeadGeneralName, namePos, new Color(240, 220, 170));
    }

    private static void DrawFormationIcon(SpriteBatch sb, Texture2D pixel, int cx, int cy, string formation)
    {
        Color iconColor = Color.White * 0.7f;
        switch (formation)
        {
            case "cavalry":
                // ^ shape (chevron)
                sb.Draw(pixel, new Rectangle(cx - 3, cy + 1, 2, 2), iconColor);
                sb.Draw(pixel, new Rectangle(cx - 1, cy - 1, 2, 2), iconColor);
                sb.Draw(pixel, new Rectangle(cx + 1, cy + 1, 2, 2), iconColor);
                break;
            case "archer":
                // > arrow shape
                sb.Draw(pixel, new Rectangle(cx - 2, cy - 2, 2, 2), iconColor);
                sb.Draw(pixel, new Rectangle(cx, cy, 2, 2), iconColor);
                sb.Draw(pixel, new Rectangle(cx - 2, cy + 2, 2, 2), iconColor);
                sb.Draw(pixel, new Rectangle(cx - 4, cy, 4, 1), iconColor);
                break;
            default: // vanguard - shield shape
                sb.Draw(pixel, new Rectangle(cx - 2, cy - 2, 4, 4), iconColor);
                sb.Draw(pixel, new Rectangle(cx - 1, cy + 2, 2, 1), iconColor);
                break;
        }
    }

    public Rectangle GetBounds()
    {
        return new Rectangle((int)ScreenPosition.X - 14, (int)ScreenPosition.Y - 26, 34, 40);
    }

    private static float EaseInOutCubic(float t)
    {
        return t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;
    }
}
