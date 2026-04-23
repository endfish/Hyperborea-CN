using Dalamud.Interface.Components;
using ECommons.ExcelServices;
using ECommons.ExcelServices.TerritoryEnumeration;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods.TerritorySelection;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
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
        var l = LayoutWorld.Instance()->ActiveLayout;
        var disableCheckbox = !Utils.CanEnablePlugin(out var DisableReasons) || Svc.Condition[ConditionFlag.Mounted];
        if (disableCheckbox) ImGui.BeginDisabled();
        if (ImGui.Checkbox($"启用 {Strings.PluginName}", ref P.Enabled))
        {
            if (P.Enabled)
            {
                SavedPos = Player.Object.Position;
                P.Memory.EnableFirewall();
                P.Memory.TargetSystem_InteractWithObjectHook.Enable();
            }
            else
            {
                Utils.Revert();
                SavedPos = null;
                SavedZoneState = null;
                P.Memory.DisableFirewall();
                P.Memory.TargetSystem_InteractWithObjectHook.Pause();
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
        ImGuiEx.Text("数据包过滤：");
        ImGui.SameLine();
        if (P.Memory.PacketDispatcher_OnSendPacketHook.IsEnabled && P.Memory.PacketDispatcher_OnReceivePacketHook.IsEnabled)
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
                ImGui.InputInt("参数 4", ref a4);
                ImGui.SetNextItemWidth(150);
                ImGui.InputInt("参数 5", ref a5);
                ImGui.SetNextItemWidth(150);
                ImGui.InputInt("CFC 覆盖值", ref CFCOverride);

                ImGui.Checkbox("覆盖出生点：", ref SpawnOverride);
                if (!SpawnOverride) ImGui.BeginDisabled();
                CoordBlock("X:", ref Position.X);
                ImGui.SameLine();
                CoordBlock("Y:", ref Position.Y);
                ImGui.SameLine();
                CoordBlock("Z:", ref Position.Z);
                if (!SpawnOverride) ImGui.EndDisabled();

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
                        SavedZoneState ??= new SavedZoneState(l->TerritoryTypeId, Player.Object.Position);
                        Utils.LoadZone((uint)a2, !SpawnOverride, true, a3, a4, a5, a6, CFCOverride);
                        if (SpawnOverride)
                        {
                            Player.GameObject->SetPosition(Position.X, Position.Y, Position.Z);
                        }
                        else if (info2 != null && info2.Spawn != null)
                        {
                            Player.GameObject->SetPosition(info2.Spawn.X, info2.Spawn.Y, info2.Spawn.Z);
                        }
                    }
                    if (disabled) ImGui.EndDisabled();
                }
                ImGui.SameLine();
                {
                    var disabled = !P.Enabled;
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
