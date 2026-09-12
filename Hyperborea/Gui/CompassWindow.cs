using ECommons.GameHelpers;
using ECommons.SimpleGui;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hyperborea.Gui;
public unsafe class CompassWindow : Window
{
    public Point3 PlayerPosition = new();
    private uint selectedGuideInstanceId;

    public CompassWindow() : base(Strings.CompassWindowTitle, ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.IsOpen = true;
        this.RespectCloseHotkey = false;
        EzConfigGui.WindowSystem.AddWindow(this);
    }

    public override bool DrawConditions()
    {
        if (P.Session?.CanNavigate != true) return false;
        var layout = Utils.GetLayout();
        Utils.TryGetZoneInfo(layout, out var info);
        if (P.Session.CanNavigate && layout != null) return true;
        return false;
    }

    public override void Draw()
    {
        var layout = Utils.GetLayout();
        Utils.TryGetZoneInfo(layout, out var info, out var isOverriden);
        if (P.Session.CanNavigate && layout != null)
        {
            var array = info?.Phases ?? [];
            var phase = Utils.GetPhase(Svc.ClientState.TerritoryType);
            var index = array.IndexOf(phase);

            ImGui.SetNextItemWidth(250f);
            if(ImGui.BeginCombo("##selphase", $"{phase?.Name.NullWhenEmpty() ?? Strings.SelectPhase}"))
            {
                foreach(var x in array)
                {
                    if (ImGui.Selectable(x.Name + $"##{x.GUID}"))
                    {
                        x.SwitchTo();
                    }
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine();

            if (array.Count < 2) ImGui.BeginDisabled(); 

            if (ImGuiEx.IconButton(FontAwesomeIcon.ArrowLeft))
            {
                if (index > 0)
                {
                    array[index - 1].SwitchTo();
                }
            }

            ImGui.SameLine();

            if (ImGuiEx.IconButton(FontAwesomeIcon.ArrowRight))
            {
                if (index < array.Count - 1)
                {
                    array[index + 1].SwitchTo();
                }
            }
            if (array.Count < 2) ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGuiEx.IconButton("\uf303"))
            {
                P.EditorWindow.IsOpen = true;
                P.EditorWindow.SelectedTerritory = Svc.ClientState.TerritoryType;
            }

            var guidePoints = P.GuideService.GetGuidePoints(layout);
            var selectedGuide = guidePoints.FirstOrDefault(x => x.InstanceId == selectedGuideInstanceId);
            ImGuiEx.TextV("副本导览：");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(250f);
            if (ImGui.BeginCombo("##guide", selectedGuide?.Name ?? "选择副本导览地点…"))
            {
                foreach (var point in guidePoints)
                {
                    if (ImGui.Selectable($"{point.Name}##guide{point.InstanceId}", point.InstanceId == selectedGuideInstanceId))
                    {
                        selectedGuideInstanceId = point.InstanceId;
                        if (P.Session.TryTeleport(point.Position.ToVector3(), out var error))
                            P.Session.Report($"已前往：{point.Name}");
                        else
                            P.Session.Report($"无法前往 {point.Name}：{error}");
                    }
                    ImGuiEx.Tooltip($"{point.Kind} / {point.LayerName}\nInstance {point.InstanceId}\nX {point.Position.X:F2}  Y {point.Position.Y:F2}  Z {point.Position.Z:F2}");
                }
                ImGui.EndCombo();
            }
            if (guidePoints.Count == 0)
                ImGuiEx.Text(EColor.YellowBright, "该区域的 PlanMap 中没有可用导览点。");
            else
                ImGuiEx.Tooltip("导览点来自本地 PlanMap LGB；选择后会通过安全坐标入口前往，不会请求服务器传送。");

            if (!C.EnableAdvancedUnsafeControls) ImGui.BeginDisabled();
            UI.CoordBlock("X:", ref PlayerPosition.X);
            ImGui.SameLine();
            UI.CoordBlock("Y:", ref PlayerPosition.Y);
            ImGui.SameLine();
            UI.CoordBlock("Z:", ref PlayerPosition.Z);
            ImGui.SameLine();
            if (ImGuiEx.IconButton("\uf3c5"))
            {
                if (!P.Session.TryTeleport(PlayerPosition.ToVector3(), out var error))
                    P.Session.Report(error);
            }
            ImGuiEx.Tooltip("传送到当前配置的坐标。");
            ImGui.SameLine();
            if (ImGuiEx.IconButton("\uf030"))
            {
                var cam = (CameraEx*)CameraManager.Instance()->GetActiveCamera();
                if (!P.Session.TryTeleport(new(cam->x, cam->y, cam->z), out var error))
                    P.Session.Report(error);
            }
            ImGuiEx.Tooltip("传送到当前镜头所在的位置。");
            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            ImGuiEx.ButtonCheckbox("\uf05b", ref C.FastTeleport);
            ImGui.PopFont();
            ImGuiEx.Tooltip("启用后可使用 CTRL + 点击，传送到鼠标指向的位置。");
            if (!C.EnableAdvancedUnsafeControls)
            {
                C.FastTeleport = false;
                ImGui.EndDisabled();
            }

            ImGui.SetNextItemWidth(200f);
            if(ImGui.BeginCombo("##mount", Utils.GetMountName(C.CurrentMount) ?? Strings.SelectMount))
            {
                ImGui.SetNextItemWidth(150f);
                ImGui.InputTextWithHint("##search", Strings.Filter, ref UI.MountFilter, 50);
                if (ImGui.Selectable(Strings.NoMount))
                {
                    C.CurrentMount = 0;
                }
                foreach(var x in Svc.Data.GetExcelSheet<Mount>())
                {
                    var name = Utils.GetMountName(x.RowId);
                    if (!name.IsNullOrEmpty())
                    {
                        if (UI.MountFilter.IsNullOrEmpty() || name.Contains(UI.MountFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            if (ImGui.Selectable(name))
                            {
                                C.CurrentMount = x.RowId;
                            }
                        }
                    }
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine();
            if (ImGuiEx.IconButton("\uf206"))
            {
                Player.Character->Mount.CreateAndSetupMount((short)(Svc.Condition[ConditionFlag.Mounted] ? 0 : C.CurrentMount), 0, 0, 0, 0, 0, 0);
            }

            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            if (!C.EnableAdvancedUnsafeControls) ImGui.BeginDisabled();
            ImGuiEx.ButtonCheckbox("\uf072", ref C.ForcedFlight);
            ImGui.PopFont();
            ImGuiEx.Tooltip("启用坐骑飞行，也允许未骑乘时使用类似坐骑的飞行。与穿模模式不兼容。");
            if (C.ForcedFlight) P.Noclip = false;
            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            ImGuiEx.ButtonCheckbox("\uf6e2", ref P.Noclip);
            ImGui.PopFont();
            ImGuiEx.Tooltip("启用穿模模式。WASD 移动，空格上升，左 Shift 下降。");
            if (P.Noclip)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                ImGuiEx.SliderFloat("##speed", ref C.NoclipSpeed, 0.05f, 0.5f);
                C.ForcedFlight = false;
            }
            if (!C.EnableAdvancedUnsafeControls)
            {
                C.ForcedFlight = false;
                P.Noclip = false;
                ImGui.EndDisabled();
            }
        }
    }
}
