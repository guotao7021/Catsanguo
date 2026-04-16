using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;
using CatSanguo.WorldMap;

namespace CatSanguo.UI;

public enum CityActionPhase
{
    CategorySelect,
    MilitaryManage,
    MilitaryDeploy,
    SelectGeneral,
    MilitarySelectTarget,
    MilitaryConfirm,
    InteriorEconomy,
    InteriorDefense,
    InteriorBuilding,
    TalentManage,
    // 新增：官员管理、外交、俘虏管理
    OfficerManage,
    DiplomacyManage,
    CaptiveManage,
}

enum SelectedCategory { None, Military, Interior, Talent, Officer, Diplomacy, Captive }
enum TalentSubTab { Discover, Persuade, Recruit }
enum OfficerSubTab { Governor, Interior, Military, Search }
enum DiplomacySubTab { View, Alliance, Trade, Ceasefire, War }

class GeneralDeployCard
{
    public GeneralData Data { get; set; } = null!;
    public bool IsSelected { get; set; }
    public string SelectedUnit { get; set; } = "步兵";
    public string SelectedFormation { get; set; } = "先锋";
    public int SoldierCount { get; set; } = 30;

    public static readonly string[] Units = { "步兵", "枪兵", "盾兵", "骑兵", "重骑", "轻骑", "弓兵", "强弩", "攻城", "法师" };
    public static readonly string[] Formations = { "先锋", "鱼鳞", "方阵", "锥形", "长蛇", "鹤翼", "八卦", "偃月", "环形" };

    public int UnitIndex { get { for (int i = 0; i < Units.Length; i++) if (Units[i] == SelectedUnit) return i; return 0; } }
    public int FormationIndex { get { for (int i = 0; i < Formations.Length; i++) if (Formations[i] == SelectedFormation) return i; return 0; } }

    public UnitType GetUnitType() => SelectedUnit switch
    {
        "步兵" => UnitType.Infantry, "枪兵" => UnitType.Spearman, "盾兵" => UnitType.ShieldInfantry,
        "骑兵" => UnitType.Cavalry, "重骑" => UnitType.HeavyCavalry, "轻骑" => UnitType.LightCavalry,
        "弓兵" => UnitType.Archer, "强弩" => UnitType.Crossbowman, "攻城" => UnitType.Siege,
        "法师" => UnitType.Mage, _ => UnitType.Infantry
    };

    public BattleFormation GetBattleFormation() => SelectedFormation switch
    {
        "先锋" => BattleFormation.Vanguard, "鱼鳞" => BattleFormation.FishScale, "方阵" => BattleFormation.Square,
        "锥形" => BattleFormation.Wedge, "长蛇" => BattleFormation.LongSnake, "鹤翼" => BattleFormation.CraneWing,
        "八卦" => BattleFormation.EightTrigrams, "偃月" => BattleFormation.CrescentMoon, "环形" => BattleFormation.Circle,
        _ => BattleFormation.Vanguard
    };
}

public class CityActionDialog
{
    // State
    public CityActionPhase Phase { get; private set; } = CityActionPhase.CategorySelect;
    public CityData? SourceCity { get; private set; }
    public string? SelectedGeneralId { get; private set; }
    public CityData? TargetCity { get; private set; }
    public List<string>? MovePath { get; private set; }
    public bool IsSelectingTarget => Phase == CityActionPhase.MilitarySelectTarget;
    public bool IsActive => SourceCity != null;
    public Vector2 WorldMousePos { get; set; }
    public Vector2 CityScreenPos { get; set; }
    public Vector2 CityWorldPos { get; set; }

    // Internal state
    private SelectedCategory _selectedCategory = SelectedCategory.None;
    private List<GeneralDeployCard> _deployCards = new();
    private TalentSubTab _currentTalentTab = TalentSubTab.Discover;
    private OfficerSubTab _currentOfficerTab = OfficerSubTab.Governor;
    private DiplomacySubTab _currentDiplomacyTab = DiplomacySubTab.View;
    private List<GeneralData> _allGeneralsRef = new();
    private List<string> _availableGenerals = new();
    private List<string> _cityGenerals = new();
    private int _selectedBuildingIndex = -1;
    private bool _officerSelectOpen = false;
    private int _officerSelectSlotIndex = -1;
    private int _currentMilitaryTab = 0; // 0=编队出征, 1=征兵管理
    private float _scrollOffset;
    private int _clickCooldown;
    private string _persuadeResultMsg = "";
    private int _persuadeResultTimer;

    // 拖拽与调整大小
    private bool _isDragging = false;
    private bool _isResizing = false;
    private Vector2 _dragOffset;
    private int _popupX, _popupY;
    private int _popupW = DefaultPopupW;
    private int _popupH = DefaultPopupH;
    private bool _positionInitialized = false;

    // Callbacks
    private Action? _onClose;
    private Action<List<string>, List<GeneralDeployEntry>, CityData>? _onLaunchArmy;
    private Action? _onOpenGeneralRoster;
    private Action? _onEndTurn;
    private Action? _onSave;
    private Func<string, string>? _getGeneralName;

    // Layout constants
    private const int DefaultPopupW = 500;
    private const int DefaultPopupH = 400;
    private const int MinPopupW = 400;
    private const int MinPopupH = 300;
    private const int NavColW = 110;
    private const int DividerW = 2;
    private int ContentW => _popupW - NavColW - DividerW;
    private const int NavBtnW = 100;
    private const int NavBtnH = 42;
    private const int ActionBtnH = 38;
    private const int HeaderH = 35;
    private const int TitleBarH = 28;
    private const int ResizeHandleSize = 14;
    private const int Pad = 5;

    // Colors - dark blue/teal theme
    private static readonly Color BgColor = new Color(20, 30, 45, 230);
    private static readonly Color NavBgColor = new Color(15, 25, 40, 240);
    private static readonly Color ContentBgColor = new Color(25, 35, 50, 220);
    private static readonly Color NavBtnNormal = new Color(30, 55, 75);
    private static readonly Color NavBtnHover = new Color(45, 80, 110);
    private static readonly Color NavBtnActive = new Color(50, 100, 140);
    private static readonly Color ActionBtnNormal = new Color(35, 60, 80);
    private static readonly Color ActionBtnHover = new Color(55, 90, 120);
    private static readonly Color BorderColor = new Color(70, 110, 140) * 0.8f;
    private static readonly Color DividerColor = new Color(50, 80, 100) * 0.6f;
    private static readonly Color TitleColor = new Color(220, 230, 240);
    private static readonly Color TextColor = new Color(180, 195, 210);
    private static readonly Color AccentColor = new Color(255, 220, 130);
    private static readonly Color DisabledColor = new Color(100, 110, 120);

    public void Initialize(Func<string, string> getGeneralName, SpriteFontBase font, SpriteFontBase titleFont, Action? onOpenGeneralRoster = null, Action? onEndTurn = null, Action? onSave = null)
    {
        _getGeneralName = getGeneralName;
        _onOpenGeneralRoster = onOpenGeneralRoster;
        _onEndTurn = onEndTurn;
        _onSave = onSave;
    }

    public void Open(CityData city, List<GeneralData> allGenerals, Action? onClose = null, Action<List<string>, List<GeneralDeployEntry>, CityData>? onLaunchArmy = null)
    {
        SourceCity = city;
        SelectedGeneralId = null;
        TargetCity = null;
        MovePath = null;
        Phase = CityActionPhase.CategorySelect;
        _selectedCategory = SelectedCategory.None;
        _allGeneralsRef = allGenerals;
        _availableGenerals = GameState.Instance.GetAvailableGeneralsForCity(city);
        _cityGenerals = GameState.Instance.GetCityGenerals(city.Id);
        _onClose = onClose;
        _onLaunchArmy = onLaunchArmy;
        _deployCards.Clear();
        _currentTalentTab = TalentSubTab.Discover;
        _selectedBuildingIndex = -1;
        _scrollOffset = 0;
        _clickCooldown = 0;
        _positionInitialized = false;
        _popupW = DefaultPopupW;
        _popupH = DefaultPopupH;
        _isDragging = false;
        _isResizing = false;
    }

    public void Close()
    {
        SourceCity = null;
        _onClose?.Invoke();
    }

    private void RefreshCityData()
    {
        if (SourceCity == null) return;
        _availableGenerals = GameState.Instance.GetAvailableGeneralsForCity(SourceCity);
        _cityGenerals = GameState.Instance.GetCityGenerals(SourceCity.Id);
        _scrollOffset = 0;
    }

    // ===================== LAYOUT CALCULATION =====================

    private Rectangle ComputePopupRect()
    {
        if (_positionInitialized)
            return new Rectangle(_popupX, _popupY, _popupW, _popupH);

        int sx = (int)CityScreenPos.X;
        int sy = (int)CityScreenPos.Y;
        int offsetX = 45;

        // Try right side first
        int px = sx + offsetX;
        if (px + _popupW > GameSettings.ScreenWidth - 10)
            px = sx - offsetX - _popupW;

        // Vertical: center on city, clamp to screen
        int py = sy - _popupH / 2;
        py = Math.Clamp(py, 60, GameSettings.ScreenHeight - 40 - _popupH);
        px = Math.Clamp(px, 10, GameSettings.ScreenWidth - 10 - _popupW);

        _popupX = px;
        _popupY = py;
        _positionInitialized = true;

        return new Rectangle(px, py, _popupW, _popupH);
    }

    private Rectangle GetNavRect(Rectangle popup) =>
        new(popup.X, popup.Y, NavColW, popup.Height);

    private Rectangle GetContentRect(Rectangle popup) =>
        new(popup.X + NavColW + DividerW, popup.Y, ContentW, popup.Height);

    // ===================== UPDATE =====================

    public void Update(InputManager input, List<CityNode> allCities)
    {
        if (_clickCooldown > 0) _clickCooldown--;
        if (_persuadeResultTimer > 0) _persuadeResultTimer--;

        var popup = ComputePopupRect();

        // --- Drag & Resize handling ---
        var mp = input.MousePosition;
        var titleBar = new Rectangle(popup.X, popup.Y, popup.Width - ResizeHandleSize, TitleBarH);
        var resizeHandle = new Rectangle(popup.Right - ResizeHandleSize, popup.Bottom - ResizeHandleSize, ResizeHandleSize, ResizeHandleSize);

        if (input.IsMouseClicked())
        {
            if (resizeHandle.Contains(mp.ToPoint()))
            {
                _isResizing = true;
                _dragOffset = new Vector2(popup.Right - mp.X, popup.Bottom - mp.Y);
            }
            else if (titleBar.Contains(mp.ToPoint()))
            {
                _isDragging = true;
                _dragOffset = new Vector2(mp.X - _popupX, mp.Y - _popupY);
            }
        }

        if (!input.IsLeftMouseHeld())
        {
            _isDragging = false;
            _isResizing = false;
        }

        if (_isDragging)
        {
            _popupX = (int)(mp.X - _dragOffset.X);
            _popupY = (int)(mp.Y - _dragOffset.Y);
            _popupX = Math.Clamp(_popupX, 0, GameSettings.ScreenWidth - _popupW);
            _popupY = Math.Clamp(_popupY, 0, GameSettings.ScreenHeight - _popupH);
            popup = new Rectangle(_popupX, _popupY, _popupW, _popupH);
        }

        if (_isResizing)
        {
            _popupW = Math.Max(MinPopupW, (int)(mp.X + _dragOffset.X) - _popupX);
            _popupH = Math.Max(MinPopupH, (int)(mp.Y + _dragOffset.Y) - _popupY);
            _popupW = Math.Min(_popupW, GameSettings.ScreenWidth - _popupX);
            _popupH = Math.Min(_popupH, GameSettings.ScreenHeight - _popupY);
            popup = new Rectangle(_popupX, _popupY, _popupW, _popupH);
        }

        if (_isDragging || _isResizing)
            return; // Don't process other interactions while dragging/resizing

        // Handle scroll in content area
        var content = GetContentRect(popup);
        if (input.IsMouseInRect(content))
        {
            int scroll = input.ScrollWheelDelta;
            if (scroll != 0)
                _scrollOffset = Math.Max(0, _scrollOffset - scroll * 0.3f);
        }

        // Always update left nav (except in target select mode)
        if (Phase != CityActionPhase.MilitarySelectTarget)
            UpdateLeftNav(input, popup);

        // Phase-specific update
        switch (Phase)
        {
            case CityActionPhase.CategorySelect:
                UpdateCategorySelect(input, popup);
                break;
            case CityActionPhase.MilitaryDeploy:
                UpdateMilitaryDeploy(input, popup);
                break;
            case CityActionPhase.SelectGeneral:
                UpdateSelectGeneral(input, popup);
                break;
            case CityActionPhase.MilitarySelectTarget:
                UpdateMilitarySelectTarget(input, allCities, popup);
                break;
            case CityActionPhase.MilitaryConfirm:
                UpdateMilitaryConfirm(input, popup);
                break;
            case CityActionPhase.InteriorEconomy:
            case CityActionPhase.InteriorDefense:
                UpdateInteriorInfo(input, popup);
                break;
            case CityActionPhase.InteriorBuilding:
                UpdateInteriorBuilding(input, popup);
                break;
            case CityActionPhase.TalentManage:
                UpdateTalentManage(input, popup);
                break;
            case CityActionPhase.OfficerManage:
                UpdateOfficerManage(input, popup);
                break;
            case CityActionPhase.MilitaryManage:
                UpdateMilitaryManage(input, popup);
                break;
        }
    }

    private void UpdateLeftNav(InputManager input, Rectangle popup)
    {
        if (!input.IsMouseClicked()) return;
        var nav = GetNavRect(popup);
        var mp = input.MousePosition.ToPoint();

        int btnX = nav.X + Pad;
        int btnY = nav.Y + 45;

        // Military button
        var milRect = new Rectangle(btnX, btnY, NavBtnW, NavBtnH);
        if (milRect.Contains(mp))
        {
            _selectedCategory = SelectedCategory.Military;
            Phase = CityActionPhase.MilitaryManage;
            _scrollOffset = 0;
            _currentMilitaryTab = 0;
            return;
        }

        // Interior button
        btnY += NavBtnH + 4;
        var intRect = new Rectangle(btnX, btnY, NavBtnW, NavBtnH);
        if (intRect.Contains(mp))
        {
            _selectedCategory = SelectedCategory.Interior;
            Phase = CityActionPhase.CategorySelect;
            _scrollOffset = 0;
            return;
        }

        // Talent button
        btnY += NavBtnH + 4;
        var talRect = new Rectangle(btnX, btnY, NavBtnW, NavBtnH);
        if (talRect.Contains(mp))
        {
            _selectedCategory = SelectedCategory.Talent;
            Phase = CityActionPhase.TalentManage;
            _scrollOffset = 0;
            _currentTalentTab = TalentSubTab.Discover;
            return;
        }

        // Officer button
        btnY += NavBtnH + 4;
        var offRect = new Rectangle(btnX, btnY, NavBtnW, NavBtnH);
        if (offRect.Contains(mp))
        {
            _selectedCategory = SelectedCategory.Officer;
            Phase = CityActionPhase.OfficerManage;
            _scrollOffset = 0;
            _officerSelectOpen = false;
            return;
        }

        // Diplomacy button
        btnY += NavBtnH + 4;
        var dipRect = new Rectangle(btnX, btnY, NavBtnW, NavBtnH);
        if (dipRect.Contains(mp))
        {
            _selectedCategory = SelectedCategory.Diplomacy;
            Phase = CityActionPhase.DiplomacyManage;
            _scrollOffset = 0;
            return;
        }

        // Captive button
        btnY += NavBtnH + 4;
        var capRect = new Rectangle(btnX, btnY, NavBtnW, NavBtnH);
        if (capRect.Contains(mp))
        {
            _selectedCategory = SelectedCategory.Captive;
            Phase = CityActionPhase.CaptiveManage;
            _scrollOffset = 0;
            return;
        }

        // 结束回合按钮（底部）
        var endTurnRect = new Rectangle(btnX, nav.Bottom - 42, NavBtnW, 36);
        if (endTurnRect.Contains(mp))
        {
            _onEndTurn?.Invoke();
            Close();
            return;
        }

        // 取消按钮（结束回合上方）
        var cancelRect = new Rectangle(btnX, nav.Bottom - 82, NavBtnW, 36);
        if (cancelRect.Contains(mp))
        {
            Close();
        }
    }

    private void UpdateCategorySelect(InputManager input, Rectangle popup)
    {
        if (!input.IsMouseClicked()) return;
        var content = GetContentRect(popup);
        var mp = input.MousePosition.ToPoint();
        int btnX = content.X + 10;
        int btnY = content.Y + HeaderH + 10;
        int btnW = ContentW - 20;

        if (_selectedCategory == SelectedCategory.Military)
        {
            if (new Rectangle(btnX, btnY, btnW, ActionBtnH).Contains(mp))
            {
                Phase = CityActionPhase.MilitaryDeploy;
                _scrollOffset = 0;
                // 进入编队时，过滤掉不在当前城池的武将
                FilterSquadBySourceCity();
                return;
            }
            btnY += ActionBtnH + 6;
            if (new Rectangle(btnX, btnY, btnW, ActionBtnH).Contains(mp))
            {
                _onOpenGeneralRoster?.Invoke();
            }
        }
        else if (_selectedCategory == SelectedCategory.Interior)
        {
            if (new Rectangle(btnX, btnY, btnW, ActionBtnH).Contains(mp))
            {
                Phase = CityActionPhase.InteriorEconomy;
                _scrollOffset = 0;
                return;
            }
            btnY += ActionBtnH + 6;
            if (new Rectangle(btnX, btnY, btnW, ActionBtnH).Contains(mp))
            {
                Phase = CityActionPhase.InteriorDefense;
                _scrollOffset = 0;
                return;
            }
            btnY += ActionBtnH + 6;
            if (new Rectangle(btnX, btnY, btnW, ActionBtnH).Contains(mp))
            {
                Phase = CityActionPhase.InteriorBuilding;
                _scrollOffset = 0;
                _selectedBuildingIndex = -1;
                return;
            }
        }
    }

    private void UpdateMilitaryDeploy(InputManager input, Rectangle popup)
    {
        if (!input.IsMouseClicked()) return;
        var content = GetContentRect(popup);
        var mp = input.MousePosition.ToPoint();

        // Back button
        var backRect = new Rectangle(content.Right - 70, content.Y + 4, 60, 28);
        if (backRect.Contains(mp)) { Phase = CityActionPhase.CategorySelect; _selectedCategory = SelectedCategory.Military; return; }

        // Remove buttons for squad slots
        var squad = GameState.Instance.CurrentSquad;
        for (int i = 0; i < squad.Count; i++)
        {
            var removeRect = new Rectangle(content.Right - 40, content.Y + HeaderH + 10 + i * 55 + 12, 30, 25);
            if (removeRect.Contains(mp))
            {
                var list = squad.ToList();
                list.RemoveAt(i);
                GameState.Instance.SetCurrentSquad(list);
                return;
            }
        }

        // Select general button
        int bottomY = content.Bottom - 50;
        var selectRect = new Rectangle(content.X + 10, bottomY, (ContentW - 30) / 2, 36);
        if (selectRect.Contains(mp))
        {
            Phase = CityActionPhase.SelectGeneral;
            _scrollOffset = 0;
            PrepareDeployCards();
            return;
        }

        // Confirm deploy button
        var confirmRect = new Rectangle(content.X + 10 + (ContentW - 30) / 2 + 10, bottomY, (ContentW - 30) / 2, 36);
        if (confirmRect.Contains(mp) && squad.Count > 0)
        {
            Phase = CityActionPhase.MilitarySelectTarget;
            TargetCity = null;
            MovePath = null;
        }
    }

    private void FilterSquadBySourceCity()
    {
        if (SourceCity == null) return;
        var gs = GameState.Instance;
        var squad = gs.CurrentSquad;
        var cityProgress = gs.GetCityProgress(SourceCity.Id);
        var filtered = new List<string>();
        foreach (var genId in squad)
        {
            var gp = gs.GetGeneralProgress(genId);
            if (gp != null && gp.CurrentCityId == SourceCity.Id)
            {
                // 排除本回合已行动的武将
                if (cityProgress?.ActedGeneralsThisTurn.Contains(genId) == true) continue;
                // 排除正在执行策反任务的武将
                if (GameState.Instance.IsOnSabotageMission(genId)) continue;
                filtered.Add(genId);
            }
        }
        gs.SetCurrentSquad(filtered);
    }

    private void PrepareDeployCards()
    {
        _deployCards.Clear();
        var currentSquad = GameState.Instance.CurrentSquad;
        var cityProgress = GameState.Instance.GetCityProgress(SourceCity?.Id ?? "");
        // Only include generals that are in the source city
        foreach (var gen in _allGeneralsRef)
        {
            var gp = GameState.Instance.GetGeneralProgress(gen.Id);
            if (gp == null || !gp.IsUnlocked) continue;
            if (gp.Status != GeneralStatus.Recruited) continue;
            // Filter: only generals in the source city can be selected
            // 双重检查：城市列表 和 CurrentCityId 都匹配才可选
            string? sourceCityId = SourceCity?.Id;
            bool inCityList = _cityGenerals.Contains(gen.Id);
            bool byCurrentCityId = !string.IsNullOrEmpty(sourceCityId) && gp.CurrentCityId == sourceCityId;
            if (!inCityList && !byCurrentCityId) continue;
            // 排除本回合已执行内政/军事任务的武将
            if (cityProgress?.ActedGeneralsThisTurn.Contains(gen.Id) == true) continue;
            // 排除正在执行策反任务的武将
            if (GameState.Instance.IsOnSabotageMission(gen.Id)) continue;
            var card = new GeneralDeployCard
            {
                Data = gen,
                IsSelected = currentSquad.Contains(gen.Id)
            };
            _deployCards.Add(card);
        }
    }

    private void UpdateSelectGeneral(InputManager input, Rectangle popup)
    {
        var content = GetContentRect(popup);
        var mp = input.MousePosition.ToPoint();

        if (input.IsMouseClicked() && _clickCooldown <= 0)
        {
            // Back button
            var backRect = new Rectangle(content.Right - 70, content.Y + 4, 60, 28);
            if (backRect.Contains(mp))
            {
                ApplyGeneralSelection();
                Phase = CityActionPhase.MilitaryDeploy;
                return;
            }

            // Confirm button at bottom
            var confirmRect = new Rectangle(content.X + 10, content.Bottom - 42, ContentW - 20, 34);
            if (confirmRect.Contains(mp))
            {
                ApplyGeneralSelection();
                Phase = CityActionPhase.MilitaryDeploy;
                return;
            }

            // General cards
            int cardH = 60;
            int startY = content.Y + HeaderH + 5;
            int visibleH = content.Height - HeaderH - 50;

            for (int i = 0; i < _deployCards.Count; i++)
            {
                int cy = startY + i * (cardH + 4) - (int)_scrollOffset;
                if (cy + cardH < startY || cy > startY + visibleH) continue;

                var card = _deployCards[i];

                // Toggle selection
                var selectArea = new Rectangle(content.X + 10, cy, ContentW - 20, 28);
                if (selectArea.Contains(mp))
                {
                    int selectedCount = _deployCards.Count(c => c.IsSelected);
                    if (card.IsSelected)
                        card.IsSelected = false;
                    else if (selectedCount < 3)
                        card.IsSelected = true;
                    _clickCooldown = 5;
                    return;
                }

                // Unit cycle (row 2 left area)
                var unitRect = new Rectangle(content.X + 40, cy + 30, 80, 24);
                if (unitRect.Contains(mp))
                {
                    int idx = (card.UnitIndex + 1) % GeneralDeployCard.Units.Length;
                    card.SelectedUnit = GeneralDeployCard.Units[idx];
                    _clickCooldown = 5;
                    return;
                }

                // Formation cycle (row 2 middle area)
                var fmtRect = new Rectangle(content.X + 130, cy + 30, 80, 24);
                if (fmtRect.Contains(mp))
                {
                    int idx = (card.FormationIndex + 1) % GeneralDeployCard.Formations.Length;
                    card.SelectedFormation = GeneralDeployCard.Formations[idx];
                    _clickCooldown = 5;
                    return;
                }

                // Soldier count -/+
                var minusRect = new Rectangle(content.X + 220, cy + 30, 24, 24);
                if (minusRect.Contains(mp))
                {
                    card.SoldierCount = Math.Max(10, card.SoldierCount - 10);
                    _clickCooldown = 5;
                    return;
                }
                var plusRect = new Rectangle(content.X + 280, cy + 30, 24, 24);
                if (plusRect.Contains(mp))
                {
                    card.SoldierCount = Math.Min(100, card.SoldierCount + 10);
                    _clickCooldown = 5;
                    return;
                }
            }
        }

        // Clamp scroll
        int totalH = _deployCards.Count * 64;
        int viewH = content.Height - HeaderH - 50;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, totalH - viewH));
    }

    private void ApplyGeneralSelection()
    {
        var selectedIds = _deployCards.Where(c => c.IsSelected).Select(c => c.Data.Id).ToList();
        GameState.Instance.SetCurrentSquad(selectedIds);

        var configs = new List<GeneralDeployEntry>();
        foreach (var card in _deployCards.Where(c => c.IsSelected))
        {
            configs.Add(new GeneralDeployEntry
            {
                GeneralId = card.Data.Id,
                UnitType = card.GetUnitType(),
                BattleFormation = card.GetBattleFormation(),
                SoldierCount = card.SoldierCount
            });
        }
        GameState.Instance.CurrentDeployConfigs = configs;
    }

    private void UpdateMilitarySelectTarget(InputManager input, List<CityNode> allCities, Rectangle popup)
    {
        if (input.IsMouseClicked())
        {
            // Cancel button (small hint bar)
            var cancelRect = new Rectangle((int)CityScreenPos.X - 40, (int)CityScreenPos.Y - 50, 80, 28);
            if (cancelRect.Contains(input.MousePosition.ToPoint()))
            {
                Phase = CityActionPhase.MilitaryDeploy;
                TargetCity = null;
                MovePath = null;
                return;
            }

            // Click on target city
            foreach (var city in allCities)
            {
                if (city.Bounds.Contains(WorldMousePos.ToPoint()) && city.Data.Id != SourceCity?.Id)
                {
                    if (SourceCity != null)
                    {
                        var path = MapPathfinder.FindPath(SourceCity.Id, city.Data.Id, allCities, "player");
                        if (path.Count >= 2)
                        {
                            TargetCity = city.Data;
                            MovePath = path;
                            Phase = CityActionPhase.MilitaryConfirm;
                        }
                    }
                    break;
                }
            }
        }
    }

    private void UpdateMilitaryConfirm(InputManager input, Rectangle popup)
    {
        if (!input.IsMouseClicked()) return;
        var content = GetContentRect(popup);
        var mp = input.MousePosition.ToPoint();

        var backRect = new Rectangle(content.Right - 70, content.Y + 4, 60, 28);
        if (backRect.Contains(mp))
        {
            Phase = CityActionPhase.MilitarySelectTarget;
            TargetCity = null;
            MovePath = null;
            return;
        }

        var confirmRect = new Rectangle(content.X + 10, content.Bottom - 50, ContentW - 20, 36);
        if (confirmRect.Contains(mp))
        {
            LaunchArmy();
        }
    }

    private void LaunchArmy()
    {
        if (SourceCity == null || TargetCity == null) return;
        var generalIds = GameState.Instance.CurrentSquad.ToList();
        if (generalIds.Count == 0) return;
        var deployConfigs = GameState.Instance.CurrentDeployConfigs.ToList();
        _onLaunchArmy?.Invoke(generalIds, deployConfigs, TargetCity);
        // 出兵后返回军事管理页面，而非关闭对话框
        Phase = CityActionPhase.MilitaryManage;
        _selectedCategory = SelectedCategory.Military;
        _currentMilitaryTab = 0;
        _deployCards.Clear();
        TargetCity = null;
        MovePath = null;
        RefreshCityData();
    }

    private void UpdateInteriorInfo(InputManager input, Rectangle popup)
    {
        if (!input.IsMouseClicked()) return;
        var content = GetContentRect(popup);
        var backRect = new Rectangle(content.Right - 70, content.Y + 4, 60, 28);
        if (backRect.Contains(input.MousePosition.ToPoint()))
        {
            Phase = CityActionPhase.CategorySelect;
            _selectedCategory = SelectedCategory.Interior;
        }
    }

    private void UpdateInteriorBuilding(InputManager input, Rectangle popup)
    {
        var content = GetContentRect(popup);
        var mp = input.MousePosition.ToPoint();

        if (!input.IsMouseClicked()) return;

        var backRect = new Rectangle(content.Right - 70, content.Y + 4, 60, 28);
        if (backRect.Contains(mp))
        {
            Phase = CityActionPhase.CategorySelect;
            _selectedCategory = SelectedCategory.Interior;
            return;
        }

        if (SourceCity == null) return;
        var progress = GameState.Instance.GetOrCreateCityProgress(SourceCity);
        var buildings = progress.Buildings.Values.ToList();

        int startY = content.Y + HeaderH + 35;
        int rowH = 45;

        for (int i = 0; i < buildings.Count; i++)
        {
            int ry = startY + i * (rowH + 4) - (int)_scrollOffset;
            var rowRect = new Rectangle(content.X + 10, ry, ContentW - 20, rowH);
            if (rowRect.Contains(mp))
            {
                if (_selectedBuildingIndex == i)
                {
                    // Click upgrade
                    var bld = buildings[i];
                    if (bld.Level < bld.MaxLevel)
                    {
                        progress.UpgradeBuilding(bld.Id, out _);
                    }
                }
                else
                {
                    _selectedBuildingIndex = i;
                }
                return;
            }
        }

        // Clamp scroll
        int totalH = buildings.Count * (rowH + 4);
        int viewH = content.Height - HeaderH - 40;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, totalH - viewH));
    }

    private void UpdateTalentManage(InputManager input, Rectangle popup)
    {
        var content = GetContentRect(popup);
        var mp = input.MousePosition.ToPoint();

        if (!input.IsMouseClicked()) return;

        // Back button
        var backRect = new Rectangle(content.Right - 70, content.Y + 4, 60, 28);
        if (backRect.Contains(mp))
        {
            Phase = CityActionPhase.CategorySelect;
            _selectedCategory = SelectedCategory.Talent;
            return;
        }

        // Tab buttons
        int tabW = (ContentW - 30) / 3;
        int tabY = content.Y + HeaderH + 5;
        for (int i = 0; i < 3; i++)
        {
            var tabRect = new Rectangle(content.X + 10 + i * (tabW + 5), tabY, tabW, 28);
            if (tabRect.Contains(mp))
            {
                _currentTalentTab = (TalentSubTab)i;
                _scrollOffset = 0;
                return;
            }
        }

        // Tab-specific actions
        int actionY = tabY + 38;
        if (_currentTalentTab == TalentSubTab.Discover)
        {
            var cp = SourceCity != null ? GameState.Instance.GetOrCreateCityProgress(SourceCity) : null;
            bool hasSearchOfficer = !string.IsNullOrEmpty(cp?.SearchOfficerId);
            bool alreadyUsed = cp?.DiscoverUsedThisTurn ?? false;
            bool searchOfficerActed = cp != null && !string.IsNullOrEmpty(cp.SearchOfficerId) && cp.ActedGeneralsThisTurn.Contains(cp.SearchOfficerId);
            bool canDiscover = hasSearchOfficer && !alreadyUsed && !searchOfficerActed;
            int btnY = actionY;
            if (!hasSearchOfficer) btnY += 20;
            else if (alreadyUsed || searchOfficerActed) btnY += 20; // 提示文字占位
            btnY += 25; // 搜索官信息占位
            var discoverRect = new Rectangle(content.X + 10, btnY, ContentW - 20, 34);
            if (discoverRect.Contains(mp) && canDiscover)
            {
                GameState.Instance.DiscoverTalent(_allGeneralsRef, SourceCity!.Id, out _, out _);
                _availableGenerals = GameState.Instance.GetAvailableGeneralsForCity(SourceCity!);
                cp!.DiscoverUsedThisTurn = true;
                cp.ActedGeneralsThisTurn.Add(cp.SearchOfficerId);
                // 搜索后直接跳转到说服标签
                _currentTalentTab = TalentSubTab.Persuade;
                _scrollOffset = 0;
            }
        }
        else if (_currentTalentTab == TalentSubTab.Persuade)
        {
            var cp = SourceCity != null ? GameState.Instance.GetOrCreateCityProgress(SourceCity) : null;
            bool hasSearchOfficer = !string.IsNullOrEmpty(cp?.SearchOfficerId);
            bool alreadyUsed = cp?.PersuadeUsedThisTurn ?? false;
            if (!hasSearchOfficer || alreadyUsed) return;

            var unaffiliated = GameState.Instance.GetAvailableTalents();
            int btnY = actionY + 25; // 搜索官信息占位

            for (int i = 0; i < unaffiliated.Count; i++)
            {
                var btnRect = new Rectangle(content.X + 10, btnY + i * 38, ContentW - 20, 32);
                if (btnRect.Contains(mp))
                {
                    bool ok = GameState.Instance.PersuadeTalent(unaffiliated[i].Data.Id, SourceCity!.Id, out var errMsg);
                    if (!ok)
                    {
                        _persuadeResultMsg = errMsg;
                        _persuadeResultTimer = 60;
                    }
                    else
                    {
                        _persuadeResultMsg = "说服成功!";
                        _persuadeResultTimer = 60;
                        cp!.PersuadeUsedThisTurn = true;
                        RefreshCityData();
                    }
                    return;
                }
            }
        }
        else if (_currentTalentTab == TalentSubTab.Recruit)
        {
            var captives = GameState.Instance.GetCaptives();
            for (int i = 0; i < captives.Count; i++)
            {
                var btnRect = new Rectangle(content.X + 10, actionY + i * 38, ContentW - 20, 32);
                if (btnRect.Contains(mp) && GameState.Instance.BattleMerit >= 150)
                {
                    GameState.Instance.RecruitCaptive(captives[i].Data.Id, out _);
                    return;
                }
            }
        }
    }

    // ===================== DRAW =====================

    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont, InputManager input)
    {
        if (!IsActive) return;

        if (Phase == CityActionPhase.MilitarySelectTarget)
        {
            DrawMilitaryTargetHint(sb, pixel, font);
            return;
        }

        var popup = ComputePopupRect();

        // Drop shadow
        sb.Draw(pixel, new Rectangle(popup.X + 4, popup.Y + 4, popup.Width, popup.Height), Color.Black * 0.3f);

        // Popup background
        sb.Draw(pixel, popup, BgColor);

        // Left nav
        var nav = GetNavRect(popup);
        sb.Draw(pixel, nav, NavBgColor);
        DrawLeftNav(sb, pixel, font, titleFont, nav, input);

        // Divider
        sb.Draw(pixel, new Rectangle(nav.Right, popup.Y + 5, DividerW, popup.Height - 10), DividerColor);

        // Right content
        var content = GetContentRect(popup);
        sb.Draw(pixel, content, ContentBgColor);

        // Border
        DrawBorder(sb, pixel, popup, BorderColor, 2);

        // Title bar drag indicator (subtle top bar)
        var titleBarRect = new Rectangle(popup.X + 2, popup.Y + 2, popup.Width - 4, TitleBarH - 2);
        sb.Draw(pixel, titleBarRect, new Color(40, 60, 80, 100));
        // Draw grip dots in center of title bar
        int gripX = popup.X + popup.Width / 2 - 15;
        int gripY = popup.Y + TitleBarH / 2 - 1;
        for (int i = 0; i < 6; i++)
            sb.Draw(pixel, new Rectangle(gripX + i * 6, gripY, 2, 2), DividerColor * 1.5f);

        // Resize handle (bottom-right corner triangle)
        var rhX = popup.Right - ResizeHandleSize;
        var rhY = popup.Bottom - ResizeHandleSize;
        for (int i = 0; i < ResizeHandleSize - 2; i++)
        {
            int lineLen = i + 1;
            sb.Draw(pixel, new Rectangle(popup.Right - lineLen - 1, rhY + i + 1, lineLen, 1), DividerColor * 0.8f);
        }

        // Phase-specific content
        switch (Phase)
        {
            case CityActionPhase.CategorySelect:
                DrawCategorySubActions(sb, pixel, font, content, input);
                break;
            case CityActionPhase.MilitaryManage:
                DrawMilitaryManage(sb, pixel, font, content, input);
                break;
            case CityActionPhase.MilitaryDeploy:
                DrawMilitaryDeploy(sb, pixel, font, content, input);
                break;
            case CityActionPhase.SelectGeneral:
                DrawSelectGeneral(sb, pixel, font, content, input);
                break;
            case CityActionPhase.MilitaryConfirm:
                DrawMilitaryConfirm(sb, pixel, font, content);
                break;
            case CityActionPhase.InteriorEconomy:
                DrawInteriorEconomy(sb, pixel, font, content);
                break;
            case CityActionPhase.InteriorDefense:
                DrawInteriorDefense(sb, pixel, font, content);
                break;
            case CityActionPhase.InteriorBuilding:
                DrawInteriorBuilding(sb, pixel, font, content, input);
                break;
            case CityActionPhase.TalentManage:
                DrawTalentManage(sb, pixel, font, content, input);
                break;
            case CityActionPhase.OfficerManage:
                DrawOfficerManage(sb, pixel, font, content, input);
                break;
            case CityActionPhase.DiplomacyManage:
                DrawDiplomacyManage(sb, pixel, font, content, input);
                break;
            case CityActionPhase.CaptiveManage:
                DrawCaptiveManage(sb, pixel, font, content, input);
                break;
        }
    }

    private void DrawLeftNav(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont, Rectangle nav, InputManager input)
    {
        var mp = input.MousePosition.ToPoint();
        string cityName = SourceCity?.Name ?? "城池";

        // City name header
        var nameSize = titleFont.MeasureString(cityName);
        sb.DrawString(titleFont, cityName, new Vector2(nav.X + (nav.Width - nameSize.X) / 2, nav.Y + 10), AccentColor);
        sb.Draw(pixel, new Rectangle(nav.X + 8, nav.Y + 38, nav.Width - 16, 1), DividerColor);

        int btnX = nav.X + Pad;
        int btnY = nav.Y + 45;

        // Category buttons
        string[] labels = { "军 事", "内 政", "人 才", "官 员", "外 交", "俘 虏" };
        SelectedCategory[] cats = { SelectedCategory.Military, SelectedCategory.Interior, SelectedCategory.Talent, SelectedCategory.Officer, SelectedCategory.Diplomacy, SelectedCategory.Captive };

        for (int i = 0; i < labels.Length; i++)
        {
            var rect = new Rectangle(btnX, btnY, NavBtnW, NavBtnH);
            bool isActive = _selectedCategory == cats[i];
            bool isHover = rect.Contains(mp);
            Color bg = isActive ? NavBtnActive : (isHover ? NavBtnHover : NavBtnNormal);
            sb.Draw(pixel, rect, bg);
            DrawBorder(sb, pixel, rect, BorderColor, 1);

            var labelSize = font.MeasureString(labels[i]);
            sb.DrawString(font, labels[i],
                new Vector2(rect.X + (rect.Width - labelSize.X) / 2, rect.Y + (rect.Height - labelSize.Y) / 2),
                isActive ? AccentColor : TitleColor);

            btnY += NavBtnH + 4;
        }

        // 结束回合按钮（底部）
        var endTurnRect = new Rectangle(btnX, nav.Bottom - 42, NavBtnW, 36);
        bool endTurnHover = endTurnRect.Contains(mp);
        sb.Draw(pixel, endTurnRect, endTurnHover ? new Color(80, 60, 40) : new Color(55, 45, 35));
        DrawBorder(sb, pixel, endTurnRect, new Color(150, 120, 80), 1);
        var endTurnSize = font.MeasureString("结束回合");
        sb.DrawString(font, "结束回合",
            new Vector2(endTurnRect.X + (endTurnRect.Width - endTurnSize.X) / 2, endTurnRect.Y + (endTurnRect.Height - endTurnSize.Y) / 2),
            endTurnHover ? new Color(255, 220, 150) : AccentColor);

        // 取消按钮（结束回合上方）
        var cancelRect = new Rectangle(btnX, nav.Bottom - 82, NavBtnW, 36);
        bool cancelHover = cancelRect.Contains(mp);
        sb.Draw(pixel, cancelRect, cancelHover ? new Color(80, 40, 40) : new Color(50, 35, 35));
        DrawBorder(sb, pixel, cancelRect, new Color(120, 70, 70) * 0.6f, 1);
        var cancelSize = font.MeasureString("取 消");
        sb.DrawString(font, "取 消",
            new Vector2(cancelRect.X + (cancelRect.Width - cancelSize.X) / 2, cancelRect.Y + (cancelRect.Height - cancelSize.Y) / 2),
            cancelHover ? new Color(255, 180, 180) : TextColor);
    }

    private void DrawCategorySubActions(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, Rectangle content, InputManager input)
    {
        var mp = input.MousePosition.ToPoint();
        int btnX = content.X + 10;
        int btnW = ContentW - 20;

        if (_selectedCategory == SelectedCategory.None)
        {
            // Show city summary
            DrawContentHeader(sb, pixel, font, content, "城池信息", false);
            if (SourceCity == null) return;
            int y = content.Y + HeaderH + 15;
            sb.DrawString(font, $"城池规模: {SourceCity.CityScale}", new Vector2(btnX, y), TextColor);
            y += 22;
            sb.DrawString(font, $"人口: {SourceCity.Population}  粮草: {SourceCity.Grain}", new Vector2(btnX, y), TextColor);
            y += 22;
            sb.DrawString(font, $"最大兵力: {SourceCity.MaxTroops}", new Vector2(btnX, y), TextColor);
            y += 22;
            sb.DrawString(font, $"防御等级: {SourceCity.DefenseLevel}  城墙: {SourceCity.WallLevel}", new Vector2(btnX, y), TextColor);
        }
        else if (_selectedCategory == SelectedCategory.Military)
        {
            DrawContentHeader(sb, pixel, font, content, "军事操作", false);
            int y = content.Y + HeaderH + 10;
            string[] actions = { "编队出征", "武将培养" };
            for (int i = 0; i < actions.Length; i++)
            {
                var rect = new Rectangle(btnX, y, btnW, ActionBtnH);
                bool hover = rect.Contains(mp);
                sb.Draw(pixel, rect, hover ? ActionBtnHover : ActionBtnNormal);
                DrawBorder(sb, pixel, rect, BorderColor, 1);
                var sz = font.MeasureString(actions[i]);
                sb.DrawString(font, actions[i],
                    new Vector2(rect.X + (rect.Width - sz.X) / 2, rect.Y + (rect.Height - sz.Y) / 2),
                    TitleColor);
                y += ActionBtnH + 6;
            }
        }
        else if (_selectedCategory == SelectedCategory.Interior)
        {
            DrawContentHeader(sb, pixel, font, content, "内政管理", false);
            int y = content.Y + HeaderH + 10;
            string[] actions = { "经济开发", "城池防御", "城池建筑" };
            for (int i = 0; i < actions.Length; i++)
            {
                var rect = new Rectangle(btnX, y, btnW, ActionBtnH);
                bool hover = rect.Contains(mp);
                sb.Draw(pixel, rect, hover ? ActionBtnHover : ActionBtnNormal);
                DrawBorder(sb, pixel, rect, BorderColor, 1);
                var sz = font.MeasureString(actions[i]);
                sb.DrawString(font, actions[i],
                    new Vector2(rect.X + (rect.Width - sz.X) / 2, rect.Y + (rect.Height - sz.Y) / 2),
                    TitleColor);
                y += ActionBtnH + 6;
            }
        }
        else if (_selectedCategory == SelectedCategory.Talent)
        {
            // Talent goes directly to TalentManage phase (handled by UpdateLeftNav)
            DrawContentHeader(sb, pixel, font, content, "人才管理", false);
        }
        else if (_selectedCategory == SelectedCategory.Officer)
        {
            // Officer now handled directly by OfficerManage phase via UpdateLeftNav
            DrawContentHeader(sb, pixel, font, content, "官员任命", false);
        }
        else if (_selectedCategory == SelectedCategory.Diplomacy)
        {
            // Diplomacy now handled directly by DiplomacyManage phase via UpdateLeftNav
            DrawContentHeader(sb, pixel, font, content, "外交关系", false);
        }
        else if (_selectedCategory == SelectedCategory.Captive)
        {
            // Captive now handled directly by CaptiveManage phase via UpdateLeftNav
            DrawContentHeader(sb, pixel, font, content, "俘虏管理", false);
        }
    }

    private void DrawMilitaryDeploy(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, Rectangle content, InputManager input)
    {
        var mp = input.MousePosition.ToPoint();
        DrawContentHeader(sb, pixel, font, content, "编队出征", true);

        var squad = GameState.Instance.CurrentSquad;
        int y = content.Y + HeaderH + 10;

        // Squad slots
        for (int i = 0; i < 3; i++)
        {
            var slotRect = new Rectangle(content.X + 10, y, ContentW - 20, 48);
            sb.Draw(pixel, slotRect, new Color(20, 30, 45, 180));
            DrawBorder(sb, pixel, slotRect, DividerColor, 1);

            if (i < squad.Count)
            {
                string genId = squad[i];
                var gen = _allGeneralsRef.FirstOrDefault(g => g.Id == genId);
                string name = gen?.Name ?? genId;
                string stats = gen != null ? $"武:{gen.Strength} 智:{gen.Intelligence} 统:{gen.Leadership}" : "";

                sb.DrawString(font, $"[{i + 1}] {name}", new Vector2(slotRect.X + 8, slotRect.Y + 5), AccentColor);
                sb.DrawString(font, stats, new Vector2(slotRect.X + 8, slotRect.Y + 25), TextColor);

                // Remove button
                var removeRect = new Rectangle(content.Right - 40, slotRect.Y + 12, 30, 25);
                bool rHover = removeRect.Contains(mp);
                sb.Draw(pixel, removeRect, rHover ? new Color(120, 50, 50) : new Color(80, 40, 40));
                sb.DrawString(font, "×", new Vector2(removeRect.X + 8, removeRect.Y + 2), new Color(255, 180, 180));
            }
            else
            {
                sb.DrawString(font, $"[{i + 1}] 空位", new Vector2(slotRect.X + 8, slotRect.Y + 14), DisabledColor);
            }
            y += 52;
        }

        // Bottom buttons
        int bottomY = content.Bottom - 50;
        int halfW = (ContentW - 30) / 2;

        var selectRect = new Rectangle(content.X + 10, bottomY, halfW, 36);
        bool selHover = selectRect.Contains(mp);
        sb.Draw(pixel, selectRect, selHover ? ActionBtnHover : ActionBtnNormal);
        DrawBorder(sb, pixel, selectRect, BorderColor, 1);
        var selSz = font.MeasureString("选择武将");
        sb.DrawString(font, "选择武将", new Vector2(selectRect.X + (halfW - selSz.X) / 2, selectRect.Y + (36 - selSz.Y) / 2), TitleColor);

        var confirmRect = new Rectangle(content.X + 10 + halfW + 10, bottomY, halfW, 36);
        bool canConfirm = squad.Count > 0;
        bool confHover = confirmRect.Contains(mp) && canConfirm;
        sb.Draw(pixel, confirmRect, confHover ? new Color(100, 60, 40) : (canConfirm ? new Color(70, 45, 30) : new Color(40, 40, 40)));
        DrawBorder(sb, pixel, confirmRect, canConfirm ? new Color(150, 100, 60) * 0.6f : DividerColor, 1);
        var confSz = font.MeasureString("确认出击");
        sb.DrawString(font, "确认出击", new Vector2(confirmRect.X + (halfW - confSz.X) / 2, confirmRect.Y + (36 - confSz.Y) / 2), canConfirm ? AccentColor : DisabledColor);
    }

    private void DrawSelectGeneral(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, Rectangle content, InputManager input)
    {
        var mp = input.MousePosition.ToPoint();
        int selectedCount = _deployCards.Count(c => c.IsSelected);
        DrawContentHeader(sb, pixel, font, content, $"选择武将 ({selectedCount}/3)", true);

        int cardH = 60;
        int startY = content.Y + HeaderH + 5;
        int visibleH = content.Height - HeaderH - 50;

        for (int i = 0; i < _deployCards.Count; i++)
        {
            int cy = startY + i * (cardH + 4) - (int)_scrollOffset;
            if (cy + cardH < startY || cy > startY + visibleH) continue;

            var card = _deployCards[i];
            var cardRect = new Rectangle(content.X + 8, cy, ContentW - 16, cardH);
            Color cardBg = card.IsSelected ? new Color(40, 70, 100, 200) : new Color(25, 35, 50, 180);
            bool cardHover = cardRect.Contains(mp);
            if (cardHover) cardBg = card.IsSelected ? new Color(50, 85, 120, 220) : new Color(35, 50, 70, 200);

            sb.Draw(pixel, cardRect, cardBg);
            DrawBorder(sb, pixel, cardRect, card.IsSelected ? new Color(100, 160, 220) * 0.5f : DividerColor, 1);

            // Row 1: selection indicator + name + stats
            string indicator = card.IsSelected ? "[✓]" : "[○]";
            sb.DrawString(font, indicator, new Vector2(cardRect.X + 6, cy + 4), card.IsSelected ? AccentColor : DisabledColor);
            sb.DrawString(font, card.Data.Name, new Vector2(cardRect.X + 38, cy + 4), TitleColor);
            string stats = $"武:{card.Data.Strength} 智:{card.Data.Intelligence} 统:{card.Data.Leadership}";
            sb.DrawString(font, stats, new Vector2(cardRect.X + 110, cy + 4), TextColor);

            // Row 2: unit / formation / soldiers
            int r2y = cy + 32;
            // Unit (clickable)
            var unitRect = new Rectangle(content.X + 40, r2y, 80, 22);
            bool uHover = unitRect.Contains(mp);
            sb.Draw(pixel, unitRect, uHover ? NavBtnHover : NavBtnNormal);
            sb.DrawString(font, card.SelectedUnit, new Vector2(unitRect.X + 4, r2y + 2), AccentColor);

            // Formation (clickable)
            var fmtRect = new Rectangle(content.X + 130, r2y, 80, 22);
            bool fHover = fmtRect.Contains(mp);
            sb.Draw(pixel, fmtRect, fHover ? NavBtnHover : NavBtnNormal);
            sb.DrawString(font, card.SelectedFormation, new Vector2(fmtRect.X + 4, r2y + 2), AccentColor);

            // Soldier count with +/-
            var minusRect = new Rectangle(content.X + 220, r2y, 24, 22);
            sb.Draw(pixel, minusRect, minusRect.Contains(mp) ? NavBtnHover : NavBtnNormal);
            sb.DrawString(font, "-", new Vector2(minusRect.X + 8, r2y + 1), TitleColor);

            sb.DrawString(font, $"{card.SoldierCount}", new Vector2(content.X + 250, r2y + 2), AccentColor);

            var plusRect = new Rectangle(content.X + 280, r2y, 24, 22);
            sb.Draw(pixel, plusRect, plusRect.Contains(mp) ? NavBtnHover : NavBtnNormal);
            sb.DrawString(font, "+", new Vector2(plusRect.X + 6, r2y + 1), TitleColor);
        }

        // Confirm button
        var confirmRect = new Rectangle(content.X + 10, content.Bottom - 42, ContentW - 20, 34);
        bool cHover = confirmRect.Contains(mp);
        sb.Draw(pixel, confirmRect, cHover ? ActionBtnHover : ActionBtnNormal);
        DrawBorder(sb, pixel, confirmRect, BorderColor, 1);
        var cSz = font.MeasureString("确认选择");
        sb.DrawString(font, "确认选择", new Vector2(confirmRect.X + (confirmRect.Width - cSz.X) / 2, confirmRect.Y + (34 - cSz.Y) / 2), TitleColor);
    }

    private void DrawMilitaryTargetHint(SpriteBatch sb, Texture2D pixel, SpriteFontBase font)
    {
        // Minimal hint bar near city
        int hintW = 220;
        int hintH = 32;
        int hx = (int)CityScreenPos.X - hintW / 2;
        int hy = (int)CityScreenPos.Y - 55;
        hx = Math.Clamp(hx, 10, GameSettings.ScreenWidth - hintW - 10);
        hy = Math.Clamp(hy, 60, GameSettings.ScreenHeight - hintH - 10);

        var hintRect = new Rectangle(hx, hy, hintW, hintH);
        sb.Draw(pixel, hintRect, BgColor);
        DrawBorder(sb, pixel, hintRect, BorderColor, 1);

        sb.DrawString(font, "点击选择目标城池", new Vector2(hx + 8, hy + 7), AccentColor);

        // Cancel button
        var cancelRect = new Rectangle(hx + hintW - 55, hy + 3, 48, 26);
        sb.Draw(pixel, cancelRect, new Color(80, 40, 40));
        sb.DrawString(font, "取消", new Vector2(cancelRect.X + 8, cancelRect.Y + 4), new Color(255, 180, 180));
    }

    private void DrawMilitaryConfirm(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, Rectangle content)
    {
        DrawContentHeader(sb, pixel, font, content, "确认出征", true);

        int y = content.Y + HeaderH + 15;
        int x = content.X + 15;

        sb.DrawString(font, $"出发: {SourceCity?.Name ?? "?"}", new Vector2(x, y), TextColor); y += 24;
        sb.DrawString(font, $"目标: {TargetCity?.Name ?? "?"}", new Vector2(x, y), AccentColor); y += 24;

        if (MovePath != null && MovePath.Count > 0)
        {
            string pathStr = string.Join("→", MovePath.Select(id => _getGeneralName != null ? GetCityName(id) : id));
            sb.DrawString(font, $"路径: {pathStr}", new Vector2(x, y), TextColor);
            y += 24;
        }

        var squad = GameState.Instance.CurrentSquad;
        string squadStr = string.Join(", ", squad.Select(id => _getGeneralName?.Invoke(id) ?? id));
        sb.DrawString(font, $"编队: {squadStr}", new Vector2(x, y), TextColor);

        // Confirm button
        var confirmRect = new Rectangle(content.X + 10, content.Bottom - 50, ContentW - 20, 36);
        sb.Draw(pixel, confirmRect, new Color(100, 60, 40));
        DrawBorder(sb, pixel, confirmRect, new Color(150, 100, 60) * 0.6f, 1);
        var sz = font.MeasureString("确认出击");
        sb.DrawString(font, "确认出击", new Vector2(confirmRect.X + (confirmRect.Width - sz.X) / 2, confirmRect.Y + (36 - sz.Y) / 2), AccentColor);
    }

    private void DrawInteriorEconomy(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, Rectangle content)
    {
        DrawContentHeader(sb, pixel, font, content, "经济开发", true);
        if (SourceCity == null) return;

        int y = content.Y + HeaderH + 15;
        int x = content.X + 15;
        var progress = GameState.Instance.GetOrCreateCityProgress(SourceCity);

        sb.DrawString(font, $"人口: {SourceCity.Population}", new Vector2(x, y), TextColor); y += 22;
        sb.DrawString(font, $"粮草: {SourceCity.Grain}", new Vector2(x, y), TextColor); y += 22;
        sb.DrawString(font, $"粮产/回合: {SourceCity.GrainProductionPerTick}", new Vector2(x, y), AccentColor); y += 22;
        sb.DrawString(font, $"兵产/回合: {SourceCity.TroopProductionPerTick}", new Vector2(x, y), AccentColor); y += 28;

        sb.DrawString(font, "资源储备:", new Vector2(x, y), TitleColor); y += 22;
        sb.DrawString(font, $"  金: {progress.GetResource(ResourceType.Gold)}", new Vector2(x, y), new Color(255, 220, 100)); y += 20;
        sb.DrawString(font, $"  粮: {progress.GetResource(ResourceType.Food)}", new Vector2(x, y), new Color(200, 180, 100)); y += 20;
        sb.DrawString(font, $"  木: {progress.GetResource(ResourceType.Wood)}", new Vector2(x, y), new Color(150, 200, 120)); y += 20;
        sb.DrawString(font, $"  铁: {progress.GetResource(ResourceType.Iron)}", new Vector2(x, y), new Color(180, 180, 200));
    }

    private void DrawInteriorDefense(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, Rectangle content)
    {
        DrawContentHeader(sb, pixel, font, content, "城池防御", true);
        if (SourceCity == null) return;

        int y = content.Y + HeaderH + 15;
        int x = content.X + 15;

        sb.DrawString(font, $"防御等级: {SourceCity.DefenseLevel}", new Vector2(x, y), TextColor); y += 22;
        sb.DrawString(font, $"城墙等级: {SourceCity.WallLevel}", new Vector2(x, y), TextColor); y += 22;
        sb.DrawString(font, $"守军加成: {(int)(SourceCity.GarrisonDefenseBonus * 100)}%", new Vector2(x, y), AccentColor); y += 22;
        sb.DrawString(font, $"最大兵力: {SourceCity.MaxTroops}", new Vector2(x, y), TextColor); y += 28;

        sb.DrawString(font, "守军:", new Vector2(x, y), TitleColor); y += 22;
        if (SourceCity.Garrison.Count == 0)
        {
            sb.DrawString(font, "  无驻军", new Vector2(x, y), DisabledColor);
        }
        else
        {
            foreach (var g in SourceCity.Garrison)
            {
                string name = _getGeneralName?.Invoke(g.GeneralId) ?? g.GeneralId;
                sb.DrawString(font, $"  {name} ({g.FormationType}) 兵:{g.SoldierCount}", new Vector2(x, y), TextColor);
                y += 20;
            }
        }
    }

    private void DrawInteriorBuilding(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, Rectangle content, InputManager input)
    {
        var mp = input.MousePosition.ToPoint();
        DrawContentHeader(sb, pixel, font, content, "城池建筑", true);
        if (SourceCity == null) return;

        var progress = GameState.Instance.GetOrCreateCityProgress(SourceCity);

        // Resource bar
        int ry = content.Y + HeaderH + 5;
        string resText = $"金:{progress.GetResource(ResourceType.Gold)} 粮:{progress.GetResource(ResourceType.Food)} 木:{progress.GetResource(ResourceType.Wood)} 铁:{progress.GetResource(ResourceType.Iron)}";
        sb.DrawString(font, resText, new Vector2(content.X + 10, ry), AccentColor);

        // Building list
        var buildings = progress.Buildings.Values.ToList();
        int startY = ry + 25;
        int rowH = 45;

        for (int i = 0; i < buildings.Count; i++)
        {
            int by = startY + i * (rowH + 4) - (int)_scrollOffset;
            if (by + rowH < startY || by > content.Bottom - 10) continue;

            var bld = buildings[i];
            var rowRect = new Rectangle(content.X + 8, by, ContentW - 16, rowH);
            bool isSelected = i == _selectedBuildingIndex;
            bool hover = rowRect.Contains(mp);
            Color bg = isSelected ? new Color(40, 70, 100, 200) : (hover ? new Color(35, 50, 70, 180) : new Color(25, 35, 50, 150));
            sb.Draw(pixel, rowRect, bg);
            DrawBorder(sb, pixel, rowRect, isSelected ? new Color(100, 160, 220) * 0.5f : DividerColor, 1);

            sb.DrawString(font, $"{bld.Name} Lv.{bld.Level}", new Vector2(rowRect.X + 8, by + 4), TitleColor);

            if (bld.Level >= bld.MaxLevel)
            {
                sb.DrawString(font, "满级", new Vector2(rowRect.X + 8, by + 24), DisabledColor);
            }
            else if (isSelected)
            {
                var config = InteriorConfig.GetBuildingConfig(bld.Id);
                if (config != null)
                {
                    int goldCost = InteriorConfig.CalculateUpgradeCost(config.GoldUpgradeCost, bld.Level + 1);
                    int foodCost = InteriorConfig.CalculateUpgradeCost(config.FoodUpgradeCost, bld.Level + 1);
                    int woodCost = InteriorConfig.CalculateUpgradeCost(config.WoodUpgradeCost, bld.Level + 1);
                    int ironCost = InteriorConfig.CalculateUpgradeCost(config.IronUpgradeCost, bld.Level + 1);
                    string costStr = $"升级: 金{goldCost} 粮{foodCost} 木{woodCost} 铁{ironCost}";
                    bool canAfford = progress.Gold >= goldCost && progress.Food >= foodCost && progress.Wood >= woodCost && progress.Iron >= ironCost;
                    sb.DrawString(font, costStr, new Vector2(rowRect.X + 8, by + 24), canAfford ? AccentColor : new Color(200, 100, 100));
                }
            }
            else
            {
                var config = InteriorConfig.GetBuildingConfig(bld.Id);
                string info = "";
                if (config != null && config.BaseProduction > 0)
                {
                    int prod = InteriorConfig.CalculateProduction(config, bld.Level, SourceCity.CityScale);
                    info = $"+{prod}/{config.ProducesResource}";
                }
                sb.DrawString(font, info, new Vector2(rowRect.X + 8, by + 24), TextColor);
            }
        }
    }

    private void DrawTalentManage(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, Rectangle content, InputManager input)
    {
        var mp = input.MousePosition.ToPoint();
        DrawContentHeader(sb, pixel, font, content, "人才管理", true);

        // Tab buttons
        int tabW = (ContentW - 30) / 3;
        int tabY = content.Y + HeaderH + 5;
        string[] tabs = { "发现", "说服", "招降" };
        for (int i = 0; i < 3; i++)
        {
            var tabRect = new Rectangle(content.X + 10 + i * (tabW + 5), tabY, tabW, 28);
            bool isActive = (int)_currentTalentTab == i;
            bool hover = tabRect.Contains(mp);
            sb.Draw(pixel, tabRect, isActive ? NavBtnActive : (hover ? NavBtnHover : NavBtnNormal));
            DrawBorder(sb, pixel, tabRect, BorderColor, 1);
            var sz = font.MeasureString(tabs[i]);
            sb.DrawString(font, tabs[i], new Vector2(tabRect.X + (tabW - sz.X) / 2, tabRect.Y + (28 - sz.Y) / 2),
                isActive ? AccentColor : TitleColor);
        }

        int actionY = tabY + 38;

        if (_currentTalentTab == TalentSubTab.Discover)
        {
            var cp = SourceCity != null ? GameState.Instance.GetOrCreateCityProgress(SourceCity) : null;
            bool hasSearchOfficer = !string.IsNullOrEmpty(cp?.SearchOfficerId);
            bool alreadyUsed = cp?.DiscoverUsedThisTurn ?? false;
            bool searchOfficerActed = cp != null && !string.IsNullOrEmpty(cp.SearchOfficerId) && cp.ActedGeneralsThisTurn.Contains(cp.SearchOfficerId);
            string searchOfficerName = hasSearchOfficer ? (_getGeneralName?.Invoke(cp!.SearchOfficerId) ?? "未知") : "未分配";

            sb.DrawString(font, $"搜索官: {searchOfficerName}", new Vector2(content.X + 15, actionY),
                hasSearchOfficer ? AccentColor : DisabledColor);
            if (!hasSearchOfficer)
            {
                actionY += 20;
                sb.DrawString(font, "(请先在官员管理中分配搜索官)", new Vector2(content.X + 15, actionY), DisabledColor);
            }
            else if (alreadyUsed)
            {
                actionY += 20;
                sb.DrawString(font, "(本回合已搜索，下回合可再次执行)", new Vector2(content.X + 15, actionY), DisabledColor);
            }
            else if (searchOfficerActed)
            {
                actionY += 20;
                sb.DrawString(font, "(搜索官本回合已执行其他任务)", new Vector2(content.X + 15, actionY), DisabledColor);
            }
            actionY += 25;

            bool canDiscover = hasSearchOfficer && !alreadyUsed && !searchOfficerActed;
            var btnRect = new Rectangle(content.X + 10, actionY, ContentW - 20, 34);
            bool bHover = btnRect.Contains(mp) && canDiscover;
            sb.Draw(pixel, btnRect, bHover ? ActionBtnHover : (canDiscover ? ActionBtnNormal : new Color(40, 40, 40)));
            DrawBorder(sb, pixel, btnRect, BorderColor, 1);
            string discText = canDiscover ? "发现人才" : (alreadyUsed ? "已搜索" : "发现人才");
            var dSz = font.MeasureString(discText);
            sb.DrawString(font, discText, new Vector2(btnRect.X + (btnRect.Width - dSz.X) / 2, btnRect.Y + (34 - dSz.Y) / 2),
                canDiscover ? TitleColor : DisabledColor);

            actionY += 44;
            sb.DrawString(font, "已发现:", new Vector2(content.X + 15, actionY), TitleColor);
            actionY += 22;

            var discovered = GameState.Instance.GetAvailableTalents();

            foreach (var gp in discovered)
            {
                sb.DrawString(font, $"  · {gp.Data.Name} [武:{gp.Data.Strength} 智:{gp.Data.Intelligence}]", new Vector2(content.X + 15, actionY), TextColor);
                actionY += 20;
            }
        }
        else if (_currentTalentTab == TalentSubTab.Persuade)
        {
            var cp = SourceCity != null ? GameState.Instance.GetOrCreateCityProgress(SourceCity) : null;
            bool hasSearchOfficer = !string.IsNullOrEmpty(cp?.SearchOfficerId);
            bool alreadyUsed = cp?.PersuadeUsedThisTurn ?? false;
            string searchOfficerName = hasSearchOfficer ? (_getGeneralName?.Invoke(cp!.SearchOfficerId) ?? "未知") : "未分配";

            sb.DrawString(font, $"搜索官: {searchOfficerName}", new Vector2(content.X + 15, actionY),
                hasSearchOfficer ? AccentColor : DisabledColor);
            actionY += 25;

            if (!hasSearchOfficer)
            {
                sb.DrawString(font, "(请先在官员管理中分配搜索官)", new Vector2(content.X + 15, actionY), DisabledColor);
            }
            else if (alreadyUsed)
            {
                sb.DrawString(font, "(本回合已说服，下回合可再次执行)", new Vector2(content.X + 15, actionY), DisabledColor);

                // 仍显示说服结果提示
                if (_persuadeResultTimer > 0 && !string.IsNullOrEmpty(_persuadeResultMsg))
                {
                    actionY += 25;
                    Color msgColor = _persuadeResultMsg.Contains("成功") ? new Color(80, 220, 80) : new Color(220, 80, 80);
                    sb.DrawString(font, _persuadeResultMsg, new Vector2(content.X + 15, actionY), msgColor);
                }
            }
            else
            {
                var unaffiliated = GameState.Instance.GetAvailableTalents();

                foreach (var gp in unaffiliated)
                {
                    int successRate = GameState.Instance.CalcPersuadeSuccessRate(cp!.SearchOfficerId, gp.Data.Id);
                    var pBtnRect = new Rectangle(content.X + 10, actionY, ContentW - 20, 32);
                    bool pHover = pBtnRect.Contains(mp);
                    sb.Draw(pixel, pBtnRect, pHover ? ActionBtnHover : ActionBtnNormal);
                    DrawBorder(sb, pixel, pBtnRect, DividerColor, 1);

                    // 检查是否有羁绊
                    bool hasBond = GameState.Instance.AllBonds.Any(b =>
                        b.RequiredGeneralIds.Contains(cp.SearchOfficerId) &&
                        b.RequiredGeneralIds.Contains(gp.Data.Id));
                    string bondMark = hasBond ? " [羁绊]" : "";
                    sb.DrawString(font, $"说服 {gp.Data.Name}{bondMark} (成功率{successRate}%)",
                        new Vector2(pBtnRect.X + 8, pBtnRect.Y + 7), TitleColor);
                    actionY += 36;
                }

                if (unaffiliated.Count == 0)
                    sb.DrawString(font, "暂无可说服的人才", new Vector2(content.X + 15, actionY), DisabledColor);

                // 显示说服结果提示
                if (_persuadeResultTimer > 0 && !string.IsNullOrEmpty(_persuadeResultMsg))
                {
                    actionY += 10;
                    Color msgColor = _persuadeResultMsg.Contains("成功") ? new Color(80, 220, 80) : new Color(220, 80, 80);
                    sb.DrawString(font, _persuadeResultMsg, new Vector2(content.X + 15, actionY), msgColor);
                    actionY += 25;
                }
                
                // 显示进行中的策反任务
                var missions = GameState.Instance.ActiveMissions;
                if (missions.Count > 0)
                {
                    actionY += 10;
                    sb.DrawString(font, "-- 进行中的策反任务 --", new Vector2(content.X + 15, actionY), new Color(200, 180, 100));
                    actionY += 22;
                    foreach (var m in missions)
                    {
                        var targetData = GameState.Instance.GetGeneralProgress(m.TargetGeneralId)?.Data;
                        string targetName = targetData?.Name ?? m.TargetGeneralId;
                        var officerData = GameState.Instance.GetGeneralProgress(m.OfficerId)?.Data;
                        string officerName = officerData?.Name ?? m.OfficerId;
                        sb.DrawString(font, $"{officerName} -> {targetName} (剩余{m.RemainingTurns}回合, {m.SuccessRate}%)",
                            new Vector2(content.X + 20, actionY), new Color(180, 160, 120));
                        actionY += 20;
                    }
                }
            }
        }
        else if (_currentTalentTab == TalentSubTab.Recruit)
        {
            sb.DrawString(font, $"战功: {GameState.Instance.BattleMerit}", new Vector2(content.X + 15, actionY), AccentColor);
            actionY += 25;

            var captives = GameState.Instance.GetCaptives();
            foreach (var cap in captives)
            {
                string name = cap.Data.Name;
                var rBtnRect = new Rectangle(content.X + 10, actionY, ContentW - 20, 32);
                bool canRecruit = GameState.Instance.BattleMerit >= 150;
                bool rHover = rBtnRect.Contains(mp) && canRecruit;
                sb.Draw(pixel, rBtnRect, rHover ? ActionBtnHover : (canRecruit ? ActionBtnNormal : new Color(40, 40, 40)));
                DrawBorder(sb, pixel, rBtnRect, DividerColor, 1);
                sb.DrawString(font, $"招降 {name} (150战功)", new Vector2(rBtnRect.X + 8, rBtnRect.Y + 7), canRecruit ? TitleColor : DisabledColor);
                actionY += 36;
            }

            if (captives.Count == 0)
                sb.DrawString(font, "暂无俘虏", new Vector2(content.X + 15, actionY), DisabledColor);
        }
    }

    // ===================== HELPERS =====================

    private void DrawContentHeader(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, Rectangle content, string title, bool showBack)
    {
        // Header background
        sb.Draw(pixel, new Rectangle(content.X, content.Y, content.Width, HeaderH), new Color(15, 25, 40, 200));

        // Title
        sb.DrawString(font, title, new Vector2(content.X + 10, content.Y + 8), TitleColor);

        // Divider
        sb.Draw(pixel, new Rectangle(content.X + 5, content.Y + HeaderH - 1, content.Width - 10, 1), DividerColor);

        if (showBack)
        {
            // Back button drawn as text (click handled in update)
            var backRect = new Rectangle(content.Right - 70, content.Y + 4, 60, 28);
            sb.Draw(pixel, backRect, new Color(50, 50, 60));
            DrawBorder(sb, pixel, backRect, DividerColor, 1);
            var sz = font.MeasureString("返回");
            sb.DrawString(font, "返回", new Vector2(backRect.X + (60 - sz.X) / 2, backRect.Y + (28 - sz.Y) / 2), TextColor);
        }
    }

    private string GetCityName(string cityId)
    {
        // Try to find city name from source city connections
        return cityId;
    }

    private static void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle rect, Color color, int thickness)
    {
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    // ===================== 官员任命 =====================

    private void UpdateOfficerManage(InputManager input, Rectangle popup)
    {
        if (!input.IsMouseClicked()) return;
        var content = GetContentRect(popup);
        var mp = input.MousePosition.ToPoint();
        var gs = GameState.Instance;
        var cp = gs.GetCityProgress(SourceCity?.Id ?? "");

        if (_officerSelectOpen)
        {
            // --- 选择面板内的交互 ---
            int panelX = content.X + 5;
            int panelW = ContentW - 10;
            int panelY = content.Y + 5;

            // "取消任命" 按钮
            string currentOfficerId = GetOfficerId(cp, _officerSelectSlotIndex);
            int dismissY = panelY + 32;
            if (!string.IsNullOrEmpty(currentOfficerId))
            {
                var dismissRect = new Rectangle(panelX + 5, dismissY, panelW - 10, 30);
                if (dismissRect.Contains(mp))
                {
                    SetOfficerId(cp, _officerSelectSlotIndex, "");
                    _officerSelectOpen = false;
                    gs.Save();
                    return;
                }
                dismissY += 36;
            }

            // 武将列表
            int listY = dismissY + 5;
            int itemH = 36;
            var cityGenIds = cp?.GeneralIds ?? new();
            for (int i = 0; i < cityGenIds.Count; i++)
            {
                int iy = listY + i * (itemH + 4) - (int)_scrollOffset;
                if (iy < panelY + 30 || iy > content.Bottom - 45) continue;
                var itemRect = new Rectangle(panelX + 5, iy, panelW - 10, itemH);
                if (itemRect.Contains(mp))
                {
                    string genId = cityGenIds[i];
                    ClearOfficerFromAllSlots(cp, genId, _officerSelectSlotIndex);
                    SetOfficerId(cp, _officerSelectSlotIndex, genId);
                    _officerSelectOpen = false;
                    gs.Save();
                    return;
                }
            }

            // "关闭" 按钮
            var closeRect = new Rectangle(panelX + 5, content.Bottom - 38, panelW - 10, 30);
            if (closeRect.Contains(mp))
            {
                _officerSelectOpen = false;
                return;
            }
        }
        else
        {
            // --- 官员槽位点击 ---
            int btnX = content.X + 10;
            int btnW = ContentW - 20;
            int y = content.Y + HeaderH + 10;
            for (int i = 0; i < 4; i++)
            {
                var rect = new Rectangle(btnX, y, btnW, 50);
                if (rect.Contains(mp))
                {
                    _officerSelectSlotIndex = i;
                    _officerSelectOpen = true;
                    _scrollOffset = 0;
                    return;
                }
                y += 56;
            }
        }
    }

    private string GetOfficerId(CityProgress? cp, int slot) => slot switch
    {
        0 => cp?.GovernorId ?? "",
        1 => cp?.InteriorOfficerId ?? "",
        2 => cp?.MilitaryOfficerId ?? "",
        3 => cp?.SearchOfficerId ?? "",
        _ => ""
    };

    private void SetOfficerId(CityProgress? cp, int slot, string genId)
    {
        if (cp == null) return;
        switch (slot)
        {
            case 0: cp.GovernorId = genId; break;
            case 1: cp.InteriorOfficerId = genId; break;
            case 2: cp.MilitaryOfficerId = genId; break;
            case 3: cp.SearchOfficerId = genId; break;
        }
    }

    private void ClearOfficerFromAllSlots(CityProgress? cp, string genId, int newSlot)
    {
        if (cp == null) return;
        // 清除所有非太守官职
        if (cp.InteriorOfficerId == genId) cp.InteriorOfficerId = "";
        if (cp.MilitaryOfficerId == genId) cp.MilitaryOfficerId = "";
        if (cp.SearchOfficerId == genId) cp.SearchOfficerId = "";
        // 只有当分配的不是太守时，才清除太守
        if (newSlot != 0 && cp.GovernorId == genId) cp.GovernorId = "";
    }

    private string GetOfficerSlotName(int slot) => slot switch
    {
        0 => "太守", 1 => "内政官", 2 => "军事官", 3 => "搜索官", _ => ""
    };

    private string GetOfficerRoleForGeneral(CityProgress? cp, string genId)
    {
        if (cp == null) return "";
        var roles = new List<string>();
        if (cp.GovernorId == genId) roles.Add("太守");
        if (cp.InteriorOfficerId == genId) roles.Add("内政官");
        if (cp.MilitaryOfficerId == genId) roles.Add("军事官");
        if (cp.SearchOfficerId == genId) roles.Add("搜索官");
        return string.Join("/", roles);
    }

    private void DrawOfficerManage(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, Rectangle content, InputManager input)
    {
        var mp = input.MousePosition.ToPoint();
        int btnX = content.X + 10;
        int btnW = ContentW - 20;

        DrawContentHeader(sb, pixel, font, content, "官员任命", false);

        int y = content.Y + HeaderH + 10;
        var gs = GameState.Instance;
        var cityProgress = gs.GetCityProgress(SourceCity?.Id ?? "");

        // 四个官员职位
        string[] officerLabels = { "太 守", "内政官", "军事官", "搜索官" };
        string[] statHints = { "统帅加成", "政治/经济加成", "兵力恢复加成", "人才发现加成" };

        for (int i = 0; i < 4; i++)
        {
            var rect = new Rectangle(btnX, y, btnW, 50);
            bool hover = rect.Contains(mp) && !_officerSelectOpen;
            sb.Draw(pixel, rect, hover ? ActionBtnHover : ActionBtnNormal);
            DrawBorder(sb, pixel, rect, BorderColor, 1);

            string officerId = GetOfficerId(cityProgress, i);
            string officerName = string.IsNullOrEmpty(officerId) ? "未任命 (点击分配)" :
                _allGeneralsRef.FirstOrDefault(g => g.Id == officerId)?.Name ?? officerId;

            sb.DrawString(font, officerLabels[i], new Vector2(rect.X + 8, rect.Y + 5), AccentColor);
            Color nameColor = string.IsNullOrEmpty(officerId) ? DisabledColor : TextColor;
            sb.DrawString(font, officerName, new Vector2(rect.X + 8, rect.Y + 25), nameColor);
            sb.DrawString(font, statHints[i], new Vector2(rect.Right - font.MeasureString(statHints[i]).X - 8, rect.Y + 15), DisabledColor);

            y += 56;
        }

        // 选择面板覆盖层
        if (_officerSelectOpen)
        {
            int panelX = content.X + 5;
            int panelW = ContentW - 10;
            int panelY = content.Y + 5;
            int panelH = content.Height - 10;
            var panelRect = new Rectangle(panelX, panelY, panelW, panelH);

            // 覆盖背景
            sb.Draw(pixel, panelRect, new Color(18, 28, 42, 245));
            DrawBorder(sb, pixel, panelRect, AccentColor, 1);

            // 标题
            string slotName = GetOfficerSlotName(_officerSelectSlotIndex);
            sb.DrawString(font, $"选择武将 - {slotName}任命", new Vector2(panelX + 8, panelY + 6), AccentColor);

            // "取消任命" 按钮（仅当有任命时显示）
            string currentOfficerId = GetOfficerId(cityProgress, _officerSelectSlotIndex);
            int listStartY = panelY + 32;
            if (!string.IsNullOrEmpty(currentOfficerId))
            {
                var dismissRect = new Rectangle(panelX + 5, listStartY, panelW - 10, 30);
                bool dismissHover = dismissRect.Contains(mp);
                sb.Draw(pixel, dismissRect, dismissHover ? new Color(120, 50, 50) : new Color(80, 35, 35));
                DrawBorder(sb, pixel, dismissRect, new Color(150, 60, 60), 1);
                var dimSz = font.MeasureString("取消任命");
                sb.DrawString(font, "取消任命",
                    new Vector2(dismissRect.X + (dismissRect.Width - dimSz.X) / 2, dismissRect.Y + (dismissRect.Height - dimSz.Y) / 2),
                    dismissHover ? new Color(255, 180, 180) : TextColor);
                listStartY += 36;
            }

            // 武将列表
            var cityGenIds = cityProgress?.GeneralIds ?? new();
            int itemH = 36;
            listStartY += 5;
            for (int i = 0; i < cityGenIds.Count; i++)
            {
                int iy = listStartY + i * (itemH + 4) - (int)_scrollOffset;
                if (iy < panelY + 30 || iy > content.Bottom - 45) continue;
                var itemRect = new Rectangle(panelX + 5, iy, panelW - 10, itemH);
                bool itemHover = itemRect.Contains(mp);
                sb.Draw(pixel, itemRect, itemHover ? new Color(45, 75, 105) : new Color(30, 50, 70));
                DrawBorder(sb, pixel, itemRect, new Color(60, 90, 120), 1);

                string genId = cityGenIds[i];
                var genData = _allGeneralsRef.FirstOrDefault(g => g.Id == genId);
                string name = genData?.Name ?? genId;

                // 显示属性（根据槽位不同）
                string stats = _officerSelectSlotIndex switch
                {
                    0 => $"统{genData?.Command ?? 0}",
                    1 => $"政{genData?.Politics ?? 0} 魅{genData?.Charisma ?? 0}",
                    2 => $"统{genData?.Command ?? 0} 武{genData?.Strength ?? 0}",
                    3 => $"智{genData?.Intelligence ?? 0}",
                    _ => ""
                };

                sb.DrawString(font, name, new Vector2(itemRect.X + 8, itemRect.Y + 8), TitleColor);
                sb.DrawString(font, stats, new Vector2(itemRect.X + 100, itemRect.Y + 8), TextColor);

                // 当前任职状态
                string role = GetOfficerRoleForGeneral(cityProgress, genId);
                if (!string.IsNullOrEmpty(role))
                {
                    string roleText = $"★{role}";
                    var roleSz = font.MeasureString(roleText);
                    sb.DrawString(font, roleText, new Vector2(itemRect.Right - roleSz.X - 8, itemRect.Y + 8), new Color(255, 200, 80));
                }

                // 本回合已行动标记
                if (cityProgress?.ActedGeneralsThisTurn.Contains(genId) == true)
                {
                    sb.DrawString(font, "[已行动]", new Vector2(itemRect.X + 180, itemRect.Y + 8), new Color(200, 100, 100));
                }
            }

            if (cityGenIds.Count == 0)
            {
                sb.DrawString(font, "该城池无驻扎武将", new Vector2(panelX + 15, listStartY + 10), DisabledColor);
            }

            // "关闭" 按钮
            var closeRect = new Rectangle(panelX + 5, content.Bottom - 38, panelW - 10, 30);
            bool closeHover = closeRect.Contains(mp);
            sb.Draw(pixel, closeRect, closeHover ? ActionBtnHover : ActionBtnNormal);
            DrawBorder(sb, pixel, closeRect, BorderColor, 1);
            var closeSz = font.MeasureString("关 闭");
            sb.DrawString(font, "关 闭",
                new Vector2(closeRect.X + (closeRect.Width - closeSz.X) / 2, closeRect.Y + (closeRect.Height - closeSz.Y) / 2),
                TitleColor);
        }
    }

    // ===================== 军事管理（子标签系统）=====================

    private void UpdateMilitaryManage(InputManager input, Rectangle popup)
    {
        if (!input.IsMouseClicked()) return;
        var content = GetContentRect(popup);
        var mp = input.MousePosition.ToPoint();

        // 子标签切换
        int tabW = (ContentW - 25) / 2;
        int tabY = content.Y + HeaderH + 5;
        for (int t = 0; t < 2; t++)
        {
            var tabRect = new Rectangle(content.X + 10 + t * (tabW + 5), tabY, tabW, 28);
            if (tabRect.Contains(mp))
            {
                _currentMilitaryTab = t;
                _scrollOffset = 0;
                return;
            }
        }

        int btnX = content.X + 10;
        int btnW = ContentW - 20;

        if (_currentMilitaryTab == 0)
        {
            // 编队出征 标签
            int y = content.Y + HeaderH + 42;
            if (new Rectangle(btnX, y, btnW, ActionBtnH).Contains(mp))
            {
                Phase = CityActionPhase.MilitaryDeploy;
                _scrollOffset = 0;
                FilterSquadBySourceCity();
                return;
            }
            y += ActionBtnH + 6;
            if (new Rectangle(btnX, y, btnW, ActionBtnH).Contains(mp))
            {
                _onOpenGeneralRoster?.Invoke();
            }
        }
        else
        {
            // 征兵管理 标签
            var gs = GameState.Instance;
            var cp = gs.GetCityProgress(SourceCity?.Id ?? "");
            if (cp == null) return;

            var cityData = DataManager.Instance.AllCities.FirstOrDefault(c => c.Id == SourceCity?.Id);
            int maxTroops = cityData?.MaxTroops ?? CityScaleConfig.GetMaxTroops(cityData?.CityScale ?? "medium");

            int y = content.Y + HeaderH + 105;

            // [-] 按钮
            var minusRect = new Rectangle(btnX + 75, y, 30, 26);
            if (minusRect.Contains(mp))
            {
                cp.RecruitTarget = Math.Max(0, cp.RecruitTarget - 10);
                return;
            }

            // [+] 按钮
            var plusRect = new Rectangle(btnX + 180, y, 30, 26);
            if (plusRect.Contains(mp))
            {
                cp.RecruitTarget = Math.Min(maxTroops, cp.RecruitTarget + 10);
                return;
            }

            // 开始/停止征兵 按钮
            y += 80;
            var toggleRect = new Rectangle(btnX, y, btnW, 36);
            if (toggleRect.Contains(mp))
            {
                if (cp.IsRecruiting)
                {
                    cp.IsRecruiting = false;
                }
                else if (cp.RecruitTarget > cp.CurrentTroops)
                {
                    cp.IsRecruiting = true;
                }
                gs.Save();
            }
        }
    }

    private void DrawMilitaryManage(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, Rectangle content, InputManager input)
    {
        var mp = input.MousePosition.ToPoint();
        DrawContentHeader(sb, pixel, font, content, "军事管理", false);

        // 子标签
        string[] tabLabels = { "编队出征", "征兵管理" };
        int tabW = (ContentW - 25) / 2;
        int tabY = content.Y + HeaderH + 5;
        for (int t = 0; t < 2; t++)
        {
            var tabRect = new Rectangle(content.X + 10 + t * (tabW + 5), tabY, tabW, 28);
            bool isActive = _currentMilitaryTab == t;
            bool hover = tabRect.Contains(mp);
            sb.Draw(pixel, tabRect, isActive ? NavBtnActive : (hover ? NavBtnHover : NavBtnNormal));
            DrawBorder(sb, pixel, tabRect, BorderColor, 1);
            var tabSz = font.MeasureString(tabLabels[t]);
            sb.DrawString(font, tabLabels[t],
                new Vector2(tabRect.X + (tabRect.Width - tabSz.X) / 2, tabRect.Y + (tabRect.Height - tabSz.Y) / 2),
                isActive ? AccentColor : TitleColor);
        }

        int btnX = content.X + 10;
        int btnW = ContentW - 20;

        if (_currentMilitaryTab == 0)
        {
            // 编队出征 标签 - 复用原有按钮
            int y = content.Y + HeaderH + 42;
            string[] actions = { "进入编队", "武将培养" };
            for (int i = 0; i < actions.Length; i++)
            {
                var rect = new Rectangle(btnX, y, btnW, ActionBtnH);
                bool hover = rect.Contains(mp);
                sb.Draw(pixel, rect, hover ? ActionBtnHover : ActionBtnNormal);
                DrawBorder(sb, pixel, rect, BorderColor, 1);
                var sz = font.MeasureString(actions[i]);
                sb.DrawString(font, actions[i],
                    new Vector2(rect.X + (rect.Width - sz.X) / 2, rect.Y + (rect.Height - sz.Y) / 2),
                    TitleColor);
                y += ActionBtnH + 6;
            }
        }
        else
        {
            // 征兵管理 标签
            var gs = GameState.Instance;
            var cp = gs.GetCityProgress(SourceCity?.Id ?? "");
            var cityData = DataManager.Instance.AllCities.FirstOrDefault(c => c.Id == SourceCity?.Id);
            int maxTroops = cityData?.MaxTroops ?? CityScaleConfig.GetMaxTroops(cityData?.CityScale ?? "medium");
            int currentTroops = cp?.CurrentTroops ?? 0;

            int y = content.Y + HeaderH + 42;

            // 兵力标题
            sb.DrawString(font, "兵力状况", new Vector2(btnX, y), AccentColor);
            y += 22;

            // 进度条
            float ratio = maxTroops > 0 ? (float)currentTroops / maxTroops : 0;
            var barRect = new Rectangle(btnX, y, btnW, 16);
            sb.Draw(pixel, barRect, new Color(15, 25, 40));
            int fillW = (int)(barRect.Width * Math.Clamp(ratio, 0f, 1f));
            if (fillW > 0)
                sb.Draw(pixel, new Rectangle(barRect.X, barRect.Y, fillW, barRect.Height), new Color(60, 160, 80));
            DrawBorder(sb, pixel, barRect, new Color(50, 80, 100), 1);
            y += 20;

            // 兵力数值
            sb.DrawString(font, $"当前兵力: {currentTroops} / {maxTroops}", new Vector2(btnX, y), TextColor);
            y += 28;

            // 分隔线
            sb.Draw(pixel, new Rectangle(btnX, y, btnW, 1), DividerColor);
            y += 8;

            // 征兵设置标题
            sb.DrawString(font, "征兵设置", new Vector2(btnX, y), AccentColor);
            y += 24;

            // 目标兵力: [-] 数值 [+]
            sb.DrawString(font, "目标兵力:", new Vector2(btnX, y + 3), TextColor);

            int recruitTarget = cp?.RecruitTarget ?? 0;
            var minusRect = new Rectangle(btnX + 75, y, 30, 26);
            bool minusHover = minusRect.Contains(mp);
            sb.Draw(pixel, minusRect, minusHover ? ActionBtnHover : ActionBtnNormal);
            DrawBorder(sb, pixel, minusRect, BorderColor, 1);
            var minusSz = font.MeasureString("−");
            sb.DrawString(font, "−", new Vector2(minusRect.X + (30 - minusSz.X) / 2, minusRect.Y + (26 - minusSz.Y) / 2), TitleColor);

            string targetStr = recruitTarget.ToString();
            var targetSz = font.MeasureString(targetStr);
            sb.DrawString(font, targetStr, new Vector2(btnX + 120 + (45 - targetSz.X) / 2, y + 3), AccentColor);

            var plusRect = new Rectangle(btnX + 180, y, 30, 26);
            bool plusHover = plusRect.Contains(mp);
            sb.Draw(pixel, plusRect, plusHover ? ActionBtnHover : ActionBtnNormal);
            DrawBorder(sb, pixel, plusRect, BorderColor, 1);
            var plusSz = font.MeasureString("+");
            sb.DrawString(font, "+", new Vector2(plusRect.X + (30 - plusSz.X) / 2, plusRect.Y + (26 - plusSz.Y) / 2), TitleColor);
            y += 32;

            // 每回合征兵量和消耗
            var barracks = cp?.GetBuilding("barracks");
            int barracksLevel = barracks?.Level ?? 0;
            int recruitPerTurn = 10 + barracksLevel * 5;

            if (cp != null && !string.IsNullOrEmpty(cp.MilitaryOfficerId))
            {
                var officer = gs.GetGeneralProgress(cp.MilitaryOfficerId);
                if (officer != null)
                    recruitPerTurn += officer.Data.Command / 10;
            }

            int goldCost = recruitPerTurn * 2;
            int grainCost = recruitPerTurn;

            sb.DrawString(font, $"每回合征兵: {recruitPerTurn} 人", new Vector2(btnX, y), TextColor);
            y += 20;
            sb.DrawString(font, $"每回合消耗: 金{goldCost}  粮{grainCost}", new Vector2(btnX, y), TextColor);
            y += 28;

            // 开始/停止征兵按钮
            bool isRecruiting = cp?.IsRecruiting ?? false;
            bool canStart = recruitTarget > currentTroops;
            var toggleRect = new Rectangle(btnX, y, btnW, 36);
            bool toggleHover = toggleRect.Contains(mp);

            if (isRecruiting)
            {
                sb.Draw(pixel, toggleRect, toggleHover ? new Color(160, 60, 60) : new Color(120, 45, 45));
                DrawBorder(sb, pixel, toggleRect, new Color(180, 80, 80), 1);
                var stopSz = font.MeasureString("停止征兵");
                sb.DrawString(font, "停止征兵",
                    new Vector2(toggleRect.X + (toggleRect.Width - stopSz.X) / 2, toggleRect.Y + (toggleRect.Height - stopSz.Y) / 2),
                    new Color(255, 200, 200));
            }
            else
            {
                Color btnBg = canStart ? (toggleHover ? new Color(50, 140, 50) : new Color(35, 100, 35)) : new Color(40, 40, 40);
                sb.Draw(pixel, toggleRect, btnBg);
                DrawBorder(sb, pixel, toggleRect, canStart ? new Color(80, 180, 80) : new Color(60, 60, 60), 1);
                var startSz = font.MeasureString("开始征兵");
                sb.DrawString(font, "开始征兵",
                    new Vector2(toggleRect.X + (toggleRect.Width - startSz.X) / 2, toggleRect.Y + (toggleRect.Height - startSz.Y) / 2),
                    canStart ? new Color(200, 255, 200) : DisabledColor);
            }
            y += 42;

            // 状态
            if (isRecruiting)
            {
                int remaining = Math.Max(0, recruitTarget - currentTroops);
                sb.DrawString(font, $"征兵中 - 还需 {remaining} 人", new Vector2(btnX, y), AccentColor);
            }
            else if (currentTroops >= maxTroops)
            {
                sb.DrawString(font, "兵力已满", new Vector2(btnX, y), new Color(100, 180, 100));
            }
            else
            {
                sb.DrawString(font, "未开始征兵", new Vector2(btnX, y), DisabledColor);
            }
        }
    }

    // ===================== 外交关系 =====================

    private void DrawDiplomacyManage(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, Rectangle content, InputManager input)
    {
        var mp = input.MousePosition.ToPoint();
        int btnX = content.X + 10;
        int btnW = ContentW - 20;
        int y = content.Y + HeaderH + 10;

        var gs = GameState.Instance;
        string playerFaction = gs.PlayerFactionId;

        // 显示当前外交关系
        sb.DrawString(font, "当前外交关系:", new Vector2(btnX, y), AccentColor);
        y += 25;

        // 演示数据：显示几个势力的关系
        string[] factions = { "曹操", "刘备", "孙权", "袁绍" };
        DiplomacyRelation[] relations = { DiplomacyRelation.Alliance, DiplomacyRelation.Neutral, DiplomacyRelation.Trade, DiplomacyRelation.Hostile };
        string[] relationTexts = { "同 盟", "中 立", "贸 易", "敌 对" };
        Color[] relationColors = { new Color(60, 130, 220), new Color(180, 195, 210), new Color(255, 220, 130), new Color(220, 60, 60) };

        for (int i = 0; i < factions.Length; i++)
        {
            var rect = new Rectangle(btnX, y, btnW, 35);
            sb.Draw(pixel, rect, new Color(20, 30, 45, 180));
            DrawBorder(sb, pixel, rect, DividerColor, 1);

            sb.DrawString(font, factions[i], new Vector2(rect.X + 8, rect.Y + 7), TextColor);
            sb.DrawString(font, relationTexts[i], new Vector2(rect.Right - 80, rect.Y + 7), relationColors[i]);

            y += 40;
        }
    }

    // ===================== 俘虏管理 =====================

    private void DrawCaptiveManage(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, Rectangle content, InputManager input)
    {
        var mp = input.MousePosition.ToPoint();
        int btnX = content.X + 10;
        int btnW = ContentW - 20;
        int y = content.Y + HeaderH + 10;

        var gs = GameState.Instance;
        var captives = gs.GetCaptives();

        if (captives.Count == 0)
        {
            sb.DrawString(font, "暂无俘虏", new Vector2(btnX, y), DisabledColor);
            return;
        }

        foreach (var captive in captives)
        {
            var rect = new Rectangle(btnX, y, btnW, 55);
            bool hover = rect.Contains(mp);
            sb.Draw(pixel, rect, hover ? ActionBtnHover : ActionBtnNormal);
            DrawBorder(sb, pixel, rect, BorderColor, 1);

            string name = captive.Data.Name;
            int loyalty = captive.Loyalty;

            sb.DrawString(font, name, new Vector2(rect.X + 8, rect.Y + 5), AccentColor);
            sb.DrawString(font, $"忠诚度: {loyalty}", new Vector2(rect.X + 8, rect.Y + 25), TextColor);

            // 招降按钮
            var recruitRect = new Rectangle(rect.Right - 85, rect.Y + 10, 75, 30);
            bool recruitHover = recruitRect.Contains(mp);
            sb.Draw(pixel, recruitRect, recruitHover ? new Color(50, 170, 50) : new Color(35, 120, 35));
            DrawBorder(sb, pixel, recruitRect, new Color(80, 200, 80) * 0.6f, 1);
            var recruitSize = font.MeasureString("招降");
            sb.DrawString(font, "招降", new Vector2(recruitRect.X + (75 - recruitSize.X) / 2, recruitRect.Y + (30 - recruitSize.Y) / 2), new Color(220, 255, 220));

            y += 60;
        }
    }
}
