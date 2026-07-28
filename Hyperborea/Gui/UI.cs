using Dalamud.Interface.Components;
using ECommons.ExcelServices;
using ECommons.ExcelServices.TerritoryEnumeration;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods.TerritorySelection;
using Lumina.Excel.Sheets;
using Hyperborea.Services;
using ECommons.ChatMethods;
using ECommons.Throttlers;
using Hyperborea.Services.OpcodeUpdaterService;

namespace Hyperborea.Gui;

public unsafe static class UI
{
    public static SavedZoneState SavedZoneState = null;
    public static Vector3? SavedPos = null;
    public static string MountFilter = "";
    static int a2 = 0;
    static int a3 = 0;
    static int a4 = 0;
    static int a5 = 1;
    internal static int a6 = 1;
    static Point3 Position = new(0,0,0);
    static bool SpawnOverride;
    static int CFCOverride = 0;

    public static void DrawNeo()
    {
        /*if(!Svc.Condition[ConditionFlag.OnFreeTrial])
        {
            ImGuiEx.TextWrapped(EColor.RedBright, "You can currently use Hyperborea only with free trial accounts. Please register free trial account and try again or wait for an update.");
            return;
        }*/
        if(!P.AllowedOperation)
        {
            ImGuiEx.TextWrapped(EColor.RedBright, "当前版本还没有可用的 opcode，请稍后再试。");
            if(ImGuiEx.Button("尝试更新 opcode", EzThrottler.Check("Opcode")))
            {
                EzThrottler.Throttle("Opcode", 60000, true);
                S.ThreadPool.Run(S.OpcodeUpdater.RunForCurrentVersion, (x) =>
                {
                    if(x != null)
                    {
                        ChatPrinter.Red(Strings.OpcodeUpdateError(x.Message));
                    }
                });
            }
            if(ImGuiEx.Button("手动输入 opcode", ImGuiEx.Ctrl))
            {
                P.DebugWindow.IsOpen = true;
            }
            ImGuiEx.Tooltip("按住 CTRL 再点击。如果填错，可能会给账号带来较高风险。");
            if (ImGui.Button("套用已知国服 ZoneDown"))
            {
                C.OpcodesZoneDown = [.. OpcodeUpdater.KnownCnZoneDownFallback];
                OpcodeUpdater.Save(false);
            }
            ImGuiEx.Tooltip($"当前内置值：{Strings.OpcodeValues(OpcodeUpdater.KnownCnZoneDownFallback)}");
            return;
        }
        var sessionTransitioning = P.Session.State is ExplorationState.Arming or ExplorationState.Reverting or ExplorationState.Faulted;
        List<string> DisableReasons = [];
        var canEnable = P.Session.IsActive || Utils.CanEnablePlugin(out DisableReasons);
        var disableCheckbox = sessionTransitioning
            || (!P.Session.IsActive && (!canEnable || Svc.Condition[ConditionFlag.Mounted]));
        if (disableCheckbox) ImGui.BeginDisabled();
        var requestedEnabled = P.Session.IsActive;
        if (ImGui.Checkbox($"启用 {Strings.PluginName}", ref requestedEnabled))
        {
            if (requestedEnabled)
            {
                if (!P.Session.TryArm(out var error))
                    P.Session.Report(error);
            }
            else
            {
                P.Session.RequestRevert(true);
            }
        }
        if (disableCheckbox)
        {
            ImGui.EndDisabled();
            if (!P.Enabled)
            {
                ImGuiEx.HelpMarker(Strings.RestrictedConditions(DisableReasons), ImGuiColors.DalamudOrange);
            }
            else
            {
                ImGuiEx.HelpMarker("禁用前你必须先下坐骑，或先执行还原。", ImGuiColors.DalamudOrange);
            }
        }
        var stateColor = P.Session.State == ExplorationState.Faulted ? EColor.RedBright
            : P.Session.State is ExplorationState.Arming or ExplorationState.Reverting ? EColor.YellowBright
            : EColor.GreenBright;
        ImGuiEx.TextWrapped(stateColor, $"安全状态：{P.Session.Status}");
        if (P.Session.HasRecoveryNotice && !P.Session.IsActive)
        {
            ImGuiEx.TextWrapped(EColor.YellowBright, $"检测到上次探索会话没有完成安全停用。记录的原区域：{C.RecoveryTerritory}。如果客户端画面仍是探索地图，请先执行恢复；只有已经回到记录区域时才能清除标记。");
            if (ImGui.Button("执行异常会话恢复"))
            {
                if (!P.Session.TryRecoverPreviousSession(out var error))
                    P.Session.Report(error);
            }
            ImGui.SameLine();
            if (ImGui.Button("确认当前状态并清除恢复标记"))
                P.Session.ClearRecoveryNotice();
        }
        ImGuiEx.Text("数据包过滤：");
        ImGui.SameLine();
        if (P.Memory.AreSafetyHooksEnabled)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            ImGuiEx.Text(EColor.GreenBright, FontAwesomeIcon.Check.ToIconString());
            ImGui.PopFont();
        }
        else
        {
            ImGui.PushFont(UiBuilder.IconFont);
            ImGuiEx.Text(EColor.RedBright, "\uf00d");
            ImGui.PopFont();
        }
        ImGuiEx.Tooltip($"启用 {Strings.PluginName} 的数据包过滤后，客户端与服务器之间的通信会被筛选，只保留维持在线、避免被踢回大厅所需的数据包。");
        ImGui.SameLine();

        ImGuiEx.Text("交互钩子：");
        ImGui.SameLine();
        if (P.Memory.TargetSystem_InteractWithObjectHook.IsEnabled)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            ImGuiEx.Text(EColor.GreenBright, FontAwesomeIcon.Check.ToIconString());
            ImGui.PopFont();
        }
        else
        {
            ImGui.PushFont(UiBuilder.IconFont);
            ImGuiEx.Text(EColor.RedBright, "\uf00d");
            ImGui.PopFont();
        }
        ImGuiEx.Tooltip($"启用 {Strings.PluginName} 的交互钩子后，你将无法与 EventNpc/EventObj 交互。");

        ImGuiEx.Text("试玩账号：");
        ImGui.SameLine();
        if (Svc.Condition[ConditionFlag.OnFreeTrial])
        {
            ImGui.PushFont(UiBuilder.IconFont);
            ImGuiEx.Text(EColor.GreenBright, FontAwesomeIcon.Check.ToIconString());
            ImGui.PopFont();
        }
        else
        {
            ImGui.PushFont(UiBuilder.IconFont);
            ImGuiEx.Text(EColor.RedBright, "\uf00d");
            ImGui.PopFont();
        }
        ImGuiEx.Tooltip($"虽然 {Strings.PluginName} 会尽量阻止客户端向服务器发送数据以提高安全性，但这并不构成任何保证，仍然建议使用试玩账号。");

        if (ImGuiGroup.BeginGroupBox())
        {
            try
            {
                ZoneInfo info = null;
                var layout = Utils.GetLayout();
                Utils.TryGetZoneInfo(layout, out info);

                var cur = ImGui.GetCursorPos();
                ImGui.SetCursorPosX(ImGuiEx.GetWindowContentRegionWidth() - ImGuiHelpers.GetButtonSize(Strings.Browse).X - ImGuiHelpers.GetButtonSize(Strings.ZoneEditor).X - 50f);
                if (ImGuiComponents.IconButtonWithText((FontAwesomeIcon)0xf002, Strings.Browse))
                {
                    new TerritorySelector((uint)a2, (sel, x) =>
                    {
                        a2 = (int)x;
                    });
                }
                ImGui.SameLine();
                if (ImGuiComponents.IconButtonWithText((FontAwesomeIcon)0xf303, Strings.ZoneEditor))
                {
                    P.EditorWindow.IsOpen = true;
                    P.EditorWindow.SelectedTerritory = (uint)a2;
                }

                ImGui.SetCursorPos(cur);
                ImGuiEx.TextV("区域数据：");
                ImGui.SetNextItemWidth(150);
                var dis = TerritorySelector.Selectors.Any(x => x.IsOpen);
                if (dis) ImGui.BeginDisabled();
                ImGui.InputInt("领地类型 ID", ref a2);
                if (dis) ImGui.EndDisabled();
                if (ExcelTerritoryHelper.NameExists((uint)a2))
                {
                    ImGuiEx.Text(ExcelTerritoryHelper.GetName((uint)a2));
                }
                ImGuiEx.Text("附加数据：");
                ImGui.SetNextItemWidth(150);
                var StoryValues = Utils.GetStoryValues((uint)a2);
                var disableda3 = !StoryValues.Any(x => x != 0);
                if (disableda3) ImGui.BeginDisabled();
                if (ImGui.BeginCombo("剧情进度", $"{a3}"))
                {
                    foreach (var x in StoryValues.Order())
                    {
                        if (ImGui.Selectable($"{x}", a3 == x)) a3 = (int)x;
                        if (a3 == x && ImGui.IsWindowAppearing()) ImGui.SetScrollHereY();
                    }
                    ImGui.EndCombo();
                }
                if (disableda3) ImGui.EndDisabled();
                if (!StoryValues.Contains((uint)a3)) a3 = (int)StoryValues.FirstOrDefault();
                ImGui.SetNextItemWidth(150);
                if (!C.EnableAdvancedUnsafeControls) ImGui.BeginDisabled();
                ImGui.InputInt("参数 4", ref a4);
                ImGui.SetNextItemWidth(150);
                ImGui.InputInt("参数 5", ref a5);
                ImGui.SetNextItemWidth(150);
                ImGui.InputInt("CFC 覆盖值", ref CFCOverride);
                if (!C.EnableAdvancedUnsafeControls)
                {
                    a4 = 0;
                    a5 = 1;
                    CFCOverride = 0;
                    ImGui.EndDisabled();
                }

                if (!C.EnableAdvancedUnsafeControls) ImGui.BeginDisabled();
                ImGui.Checkbox("覆盖出生点：", ref SpawnOverride);
                if (!SpawnOverride) ImGui.BeginDisabled();
                CoordBlock("X:", ref Position.X);
                ImGui.SameLine();
                CoordBlock("Y:", ref Position.Y);
                ImGui.SameLine();
                CoordBlock("Z:", ref Position.Z);
                if (!SpawnOverride) ImGui.EndDisabled();
                if (!C.EnableAdvancedUnsafeControls)
                {
                    SpawnOverride = false;
                    ImGui.EndDisabled();
                }

                ImGuiHelpers.ScaledDummy(3f);
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(3f);

                {
                    var size = ImGuiEx.CalcIconSize("\uf3c5", true);
                    size += ImGuiEx.CalcIconSize("\uf15c", true);
                    size += ImGuiEx.CalcIconSize(FontAwesomeIcon.Cog, true);
                    size.X += ImGui.GetStyle().ItemSpacing.X * 3;

                    var cur2 = ImGui.GetCursorPos();
                    ImGui.SetCursorPosX(ImGuiEx.GetWindowContentRegionWidth() - size.X);
                    var disabled = !Utils.CanUse();
                    if (disabled) ImGui.BeginDisabled();
                    if (ImGuiEx.IconButton(FontAwesomeIcon.Compass))
                    {
                        P.CompassWindow.IsOpen = !P.CompassWindow.IsOpen;
                    }
                    if (disabled) ImGui.EndDisabled();
                    ImGui.SameLine();
                    if (ImGuiEx.IconButton("\uf15c"))
                    {
                        P.LogWindow.IsOpen = true;
                    }
                    ImGui.SameLine();
                    if (ImGuiEx.IconButton(FontAwesomeIcon.Cog))
                    {
                        P.SettingsWindow.IsOpen = true;
                    }
                    ImGui.SetCursorPos(cur2);
                }

                {
                    var disabled = !Utils.CanUse();
                    if (disabled) ImGui.BeginDisabled();
                    if (ImGui.Button(Strings.LoadZone))
                    {
                        Utils.TryGetZoneInfo(Utils.GetLayout((uint)a2), out var info2);
                        var destination = SpawnOverride ? Position : info2?.Spawn;
                        if (destination != null && !P.Session.ValidatePosition(destination.ToVector3(), out var error))
                        {
                            P.Session.Report($"拒绝加载区域：{error}");
                        }
                        else
                        {
                            Utils.LoadZone((uint)a2, false, true, a3, a4, a5, a6, CFCOverride, destination);
                        }
                    }
                    if (disabled) ImGui.EndDisabled();
                }
                ImGui.SameLine();
                {
                    var disabled = P.Session.State != ExplorationState.Exploring;
                    if (disabled) ImGui.BeginDisabled();
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Undo, Strings.Revert))
                    {
                        Utils.Revert();
                    }
                    if (disabled) ImGui.EndDisabled();
                }
            }
            catch(Exception e)
            {
                ImGuiEx.Text(e.ToString());
            }
            ImGuiGroup.EndGroupBox();
        }
    }
    internal static void CoordBlock(string t, ref float p)
    {
        ImGuiEx.TextV(t);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(60f);
        ImGui.DragFloat("##" + t, ref p, 0.1f);
    }
}
