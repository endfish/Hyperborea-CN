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
                        foreach (var x in p.MapEffects)
                        {
                            ImGui.PushID(x.GUID);
                            ImGui.SetNextItemWidth(100f);
                            ImGui.InputInt($"##a1", ref x.a1);
                            ImGui.SameLine();
                            ImGui.SetNextItemWidth(100f);
                            ImGui.InputInt($"##a2", ref x.a2);
                            ImGui.SameLine();
                            ImGui.SetNextItemWidth(100f);
                            ImGui.InputInt($"##a3", ref x.a3);
                            ImGui.SameLine();
                            if (ImGui.Button(Strings.Delete))
                            {
                                new TickScheduler(() => p.MapEffects.RemoveAll(z => z.GUID == x.GUID));
                            }
                            ImGui.PopID();
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
