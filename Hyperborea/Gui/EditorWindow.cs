using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using ECommons.Configuration;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods.TerritorySelection;
using ECommons.SimpleGui;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hyperborea.Gui;
public unsafe class EditorWindow : Window
{
    Dictionary<string, HashSet<uint>> BgToTerritoryType = [];
    internal uint SelectedTerritory = 0;
    uint TerrID => SelectedTerritory == 0 ? Svc.ClientState.TerritoryType : SelectedTerritory;
    public EditorWindow() : base(Strings.EditorWindowTitle)
    {
        EzConfigGui.WindowSystem.AddWindow(this);
        foreach(var x in Svc.Data.GetExcelSheet<TerritoryType>())
        {
            var bg = ((TerritoryType?)x).GetBG();
            if (!bg.IsNullOrEmpty())
            {
                if(!BgToTerritoryType.TryGetValue(bg, out var list))
                {
                    list = [];
                    BgToTerritoryType[bg] = list;
                }
                list.Add(x.RowId);
            }
        }
    }

    public override void Draw()
    {
        var cur = ImGui.GetCursorPos();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.SetCursorPosX(ImGuiEx.GetWindowContentRegionWidth() - ImGui.CalcTextSize("\uf0c7").X);
        if(Utils.TryGetZoneInfo(ExcelTerritoryHelper.GetBG(TerrID), out _, out var isOverriden1))
        {
            if (isOverriden1)
            {
                ImGuiEx.Text(EColor.YellowBright, $"\uf0c7");
                ImGui.PopFont();
                ImGuiEx.Tooltip("当前区域数据来自你的覆盖配置文件。");
            }
            else
            {
                ImGuiEx.Text(EColor.GreenBright, $"\uf0c7");
                ImGui.PopFont();
                ImGuiEx.Tooltip("当前区域数据来自主数据文件。");
            }
        }
        else
        {
            ImGuiEx.Text(EColor.RedBright, $"\uf0c7");
            ImGui.PopFont();
            ImGuiEx.Tooltip("主数据和覆盖数据中都没有找到这个区域的配置。");
        }
        ImGui.SetCursorPos(cur);
        var shares = BgToTerritoryType.TryGetValue(ExcelTerritoryHelper.GetBG(TerrID), out var set) ? set : [];
        ImGuiEx.TextWrapped(Strings.EditingZone(ExcelTerritoryHelper.GetName(TerrID, true)));
        if(shares.Count > 1)
        {
            ImGuiComponents.HelpMarker(Strings.SharedDataWith(shares.Where(z => z != TerrID).Select(z => ExcelTerritoryHelper.GetName(z, true))));
        }
        if (ImGuiComponents.IconButtonWithText((FontAwesomeIcon)0xf002, Strings.Browse))
        {
            new TerritorySelector(SelectedTerritory, (_, x) =>
            {
                SelectedTerritory = x;
            });
        }
        ImGui.SameLine();
        if(ImGuiComponents.IconButtonWithText((FontAwesomeIcon)0xf276, Strings.CurrentZone))
        {
            SelectedTerritory = 0;
        }

        var bg = ExcelTerritoryHelper.GetBG(TerrID);
        if (bg.IsNullOrEmpty())
        {
            ImGuiEx.Text("当前区域不受支持。");
        }
        else
        {
            if (Utils.TryGetZoneInfo(bg, out var info, out var isOverriden))
            {
                var overrideSpawn = info.Spawn != null;
                if (ImGui.Checkbox("自定义出生点", ref overrideSpawn))
                {
                    info.Spawn = overrideSpawn ? new() : null;
                }
                if (overrideSpawn)
                {
                    UI.CoordBlock("X:", ref info.Spawn.X);
                    ImGui.SameLine();
                    UI.CoordBlock("Y:", ref info.Spawn.Y);
                    ImGui.SameLine();
                    UI.CoordBlock("Z:", ref info.Spawn.Z);
                    ImGui.SameLine();
                    if (ImGuiEx.IconButton("\uf3c5")) info.Spawn = Player.Object.Position.ToPoint3();
                    ImGuiEx.Tooltip("将区域出生点设置为角色当前位置。");
                }
                ImGui.Separator();
                ImGuiEx.TextV("阶段：");
                ImGui.SameLine();
                if (ImGuiEx.IconButton(FontAwesome.Plus))
                {
                    info.Phases.Add(new());
                }
                ImGuiEx.Tooltip("新建一个阶段。");
                foreach (var p in info.Phases)
                {
                    ImGui.PushID(p.GUID);
                    if (ImGui.CollapsingHeader($"{p.Name}###phase"))
                    {
                        ImGuiEx.TextV("名称：");
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(150f);
                        ImGui.InputText($"##Name", ref p.Name, 20);
                        ImGui.SameLine();
                        if (ImGuiEx.IconButton(FontAwesome.Trash) && ImGuiEx.Ctrl)
                        {
                            new TickScheduler(() => info.Phases.RemoveAll(z => z.GUID == p.GUID));
                        }
                        ImGuiEx.Tooltip("按住 CTRL 删除该阶段。");
                        ImGuiEx.TextV("天气：");
                        ImGui.SameLine();
                        if (ImGui.BeginCombo("##Weather", $"{Utils.GetWeatherName(p.Weather)}"))
                        {
                            foreach (var x in (uint[])[0, .. P.Weathers[TerrID]])
                            {
                                if (ImGui.Selectable($"{x} - {Utils.GetWeatherName(x)}"))
                                {
                                    if (P.Enabled && Svc.ClientState.TerritoryType == TerrID && Utils.GetPhase(Svc.ClientState.TerritoryType) == p)
                                    {
                                        EnvManager.Instance()->ActiveWeather = (byte)x;
                                        EnvManager.Instance()->TransitionTime = 0.5f;
                                    }
                                    p.Weather = x;
                                }
                            }
                            ImGui.EndCombo();
                        }
                        ImGuiEx.TextV("地图效果：");
                        ImGui.SameLine();
                        if (ImGuiEx.IconButton(FontAwesome.Plus))
                        {
                            p.MapEffects.Add(new());
                        }
                        ImGuiEx.Tooltip("添加一个新的地图效果。");
                        ImGui.SameLine();
                        if (ImGuiEx.IconButton(FontAwesomeIcon.Copy))
                        {
                            Copy(P.YamlFactory.Serialize(p.MapEffects, true));
                        }
                        ImGuiEx.Tooltip("复制这个阶段当前配置的地图效果。");
                        ImGui.SameLine();
                        if (ImGuiEx.IconButton(FontAwesomeIcon.Paste))
                        {
                            Safe(() => p.MapEffects = P.YamlFactory.Deserialize<List<MapEffectInfo>>(Paste()));
                        }
                        ImGuiEx.Tooltip("粘贴并覆盖这个阶段的地图效果。");
                        var slots = MapEffectResolver.GetZoneSlots(TerrID);
                        foreach (var x in p.MapEffects)
                        {
                            ImGui.PushID(x.GUID);

                            var isDupe = p.MapEffects.Count(y => y.Slot == x.Slot) > 1;
                            if (isDupe)
                            {
                                ImGuiEx.Text(EColor.RedBright, "[槽位重复]");
                                ImGuiEx.Tooltip("这个阶段中已有其他效果使用同一槽位，实际只会应用列表中最后一项。");
                                ImGui.SameLine();
                            }

                            var curSlot = slots.FirstOrDefault(s => s.Slot == x.Slot);
                            ImGui.SetNextItemWidth(180f);
                            if (ImGui.BeginCombo("##slot", curSlot != null ? $"{x.Slot}: {curSlot.SgbName}" : $"槽位 {x.Slot}（未知）"))
                            {
                                foreach (var s in slots)
                                {
                                    if (s.Slot != x.Slot && p.MapEffects.Any(y => y.GUID != x.GUID && y.Slot == s.Slot)) continue;

                                    if (ImGui.Selectable($"{s.Slot}: {s.SgbName}##slot{s.Slot}", x.Slot == s.Slot))
                                    {
                                        x.Slot = s.Slot;
                                        x.State = 0;
                                        x.TimelineOverride = 0;
                                    }
                                }
                                ImGui.EndCombo();
                            }
                            ImGuiEx.Tooltip("选择要控制的地图效果槽位。");
                            ImGui.SameLine();

                            var states = curSlot?.States ?? [];
                            var curStateName = states.FirstOrDefault(st => st.State == (ushort)x.State).Name;
                            ImGui.SetNextItemWidth(180f);
                            if (ImGui.BeginCombo("##state", curStateName.NullWhenEmpty() != null ? $"{x.State}: {curStateName}" : $"状态 {x.State}"))
                            {
                                if (ImGui.Selectable("0（不操作）", x.State == 0)) x.State = 0;
                                foreach (var (state, name) in states)
                                {
                                    if (ImGui.Selectable($"{state}: {name}##st{state}", x.State == state)) x.State = state;
                                }
                                ImGui.EndCombo();
                            }
                            ImGuiEx.Tooltip("设置该槽位的状态。默认情况下，播放的时间轴也会跟随这个状态。");
                            ImGui.SameLine();

                            if (ImGuiEx.IconButton(FontAwesomeIcon.Cog))
                            {
                                ImGui.SetNextWindowPos(new Vector2(ImGui.GetItemRectMin().X, ImGui.GetItemRectMax().Y));
                                ImGui.OpenPopup("##advTimeline");
                            }
                            ImGuiEx.Tooltip("高级：独立于状态，覆盖当前播放的时间轴。\n默认保持为 0。只有需要在同一槽位组合多个独立效果（例如火把和闸门），或触发过渡动画时才需要修改。");
                            ImGui.SetNextWindowSize(new Vector2(280f, 0f), ImGuiCond.Appearing);
                            if (ImGui.BeginPopup("##advTimeline"))
                            {
                                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 260f);
                                ImGuiEx.TextWrapped("选择当前播放或停止的时间轴。全部取消勾选时，时间轴跟随上方状态（默认设置）。");
                                ImGui.PopTextWrapPos();
                                if (ImGui.Selectable("恢复默认（跟随状态）")) x.TimelineOverride = 0;
                                ImGui.Separator();
                                foreach (var (state, name) in states)
                                {
                                    var on = ((ushort)x.TimelineOverride & state) != 0;
                                    if (ImGui.Checkbox($"{state}: {name}##tl{state}", ref on))
                                    {
                                        x.TimelineOverride = on ? (x.TimelineOverride | state) : (x.TimelineOverride & ~state);
                                    }
                                }
                                ImGui.EndPopup();
                            }
                            if (x.TimelineOverride != 0)
                            {
                                ImGui.SameLine();
                                var curTimelineNames = states.Where(st => ((ushort)x.TimelineOverride & st.State) != 0).Select(st => st.Name).ToList();
                                ImGuiEx.Text(EColor.YellowBright, curTimelineNames.Count > 0 ? string.Join(", ", curTimelineNames) : $"{x.TimelineOverride}");
                                ImGuiEx.Tooltip("正在覆盖时间轴，不再跟随状态。");
                            }
                            ImGui.SameLine();
                            if (ImGui.Button(Strings.Delete))
                            {
                                new TickScheduler(() => p.MapEffects.RemoveAll(z => z.GUID == x.GUID));
                            }
                            ImGui.PopID();
                        }
                        if (slots.Count == 0)
                        {
                            ImGui.TextDisabled("当前区域没有找到地图效果槽位。");
                        }
                        ImGuiEx.TextV(Strings.Festivals);
                        var zoneFests = Utils.GetZoneFestivals(bg);
                        if (zoneFests.Count == 0)
                        {
                            ImGui.SameLine();
                            ImGui.TextDisabled(Strings.NoFestivalsInZone);
                        }
                        foreach (var (festivalId, festivalPhases) in zoneFests)
                        {
                            DrawFestivalRow(p, festivalId, festivalPhases);
                        }
                    }
                    ImGui.PopID();
                }
                if (ImGui.Button(Strings.Save))
                {
                    Utils.CreateZoneInfoOverride(bg, info.JSONClone(), true);
                    P.SaveZoneData();
                }
                if(isOverriden)
                {
                    if(ImGui.Button(Strings.Reset))
                    {
                        Utils.LoadBuiltInZoneData();
                        new TickScheduler(() =>
                        {
                            P.ZoneData.Data.Remove(bg);
                            P.SaveZoneData();
                        });
                    }
                }
            }
            else
            {
                ImGuiEx.Text("未找到数据。");
                if (ImGui.Button("创建覆盖配置"))
                {
                    Utils.CreateZoneInfoOverride(bg, new()
                    {
                        Name = ExcelTerritoryHelper.GetName(TerrID),
                    });
                }
            }
        }
    }

    void DrawFestivalRow(PhaseInfo p, int festivalId, List<int> festivalPhaseIds)
    {
        using var id = ImRaii.PushId(festivalId);

        var data = P.FestivalDatas.FirstOrDefault(z => z.Id == festivalId);
        var selected = p.Festivals.FirstOrDefault(z => z.Id == festivalId);
        var IsMulti = festivalPhaseIds.Any(x => x != 0);

        ImGui.SetNextItemWidth(160f);
        if (ImGui.BeginCombo($"##Festival", PhaseLabel(data, selected?.PhaseId, IsMulti)))
        {
            foreach (var phaseId in (int[])[-1, .. festivalPhaseIds])
            {
                if (ImGui.Selectable(PhaseLabel(data, phaseId, IsMulti), selected?.PhaseId == phaseId))
                {
                    if (phaseId < 0)
                    {
                        p.Festivals.RemoveAll(z => z.Id == festivalId);
                    }
                    else if (selected == null)
                    {
                        selected = new() { Id = festivalId, PhaseId = phaseId };
                        p.Festivals.Add(selected);
                    }
                    else
                    {
                        selected.PhaseId = phaseId;
                    }
                    if (P.Enabled && Svc.ClientState.TerritoryType == TerrID && Utils.GetPhase(Svc.ClientState.TerritoryType) == p)
                    {
                        P.ApplyFestivals(p.Festivals);
                    }
                }
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        ImGuiEx.Text(data?.Name.NullWhenEmpty() ?? $"#{festivalId}");
    }

    static string PhaseLabel(FestivalData data, int? phaseId, bool isMulti)
    {
        if (phaseId < 0 || phaseId == null) return Strings.FestivalOff;
        if (!isMulti) return Strings.FestivalOn;
        var pname = data?.Phases.FirstOrDefault(z => z.Id == phaseId)?.Name;
        if (!pname.IsNullOrEmpty()) return $"{phaseId} - {pname}";
        return Strings.FestivalPhase(phaseId.Value);
    }
}
