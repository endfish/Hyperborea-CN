using Dalamud.Memory;
using ECommons.Configuration;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.Hooks;
using ECommons.Opcodes;
using ECommons.Reflection;
using ECommons.SimpleGui;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using Hyperborea.Services.OpcodeUpdaterService;
using Lumina.Excel.Sheets;
using System.Windows.Forms;
using OpcodeUpdater = Hyperborea.Services.OpcodeUpdaterService.OpcodeUpdater;

namespace Hyperborea.Gui;
public unsafe class DebugWindow: Window
{
    public DebugWindow() : base(Strings.DebugWindowTitle)
    {
        EzConfigGui.WindowSystem.AddWindow(this);
    }

    int i1, i2, i3, i4;
    uint i5, i6, i7;

    public override void Draw()
    {
        ImGuiEx.EzTabBar("Tabs", [
            ("Opcode", DrawOpcodes, null, true),
            ("调试", DrawDebug, null, true)
            ]);
    }

    void DrawOpcodes()
    {
        ImGui.Checkbox("禁用 opcode 自动更新", ref C.ManualOpcodeManagement);
        ImGuiEx.HelpMarker($"启用后，{Strings.PluginName} 将不再尝试自动更新 opcode，之后每次游戏更新都需要你手动修改。仅临时编辑一次 opcode 时，不需要勾选这个选项。");
        ImGuiEx.TextWrapped($"当前 ZoneDown：{(C.OpcodesZoneDown.Length > 0 ? Strings.OpcodeValues(C.OpcodesZoneDown) : "未设置")}");

        ImGuiEx.TextWrapped($"""
                请输入 ZoneDown opcode。
                获取方法：前往{Strings.InnRoomExample}，输入 /xldata network，然后在不做任何操作的情况下等待片刻。你应该会看到两个 Direction=ZoneDown 且按固定时间间隔重复出现的 opcode，把 OpCode 列中的值填进来。
                支持十进制和十六进制，像 0x3C9,0x2D8 这样直接粘贴即可。
                """);
        EditOpcodes("##zoneDown", ref C.OpcodesZoneDown);
        if (ImGui.Button("套用已知国服 ZoneDown"))
        {
            C.OpcodesZoneDown = [.. OpcodeUpdater.KnownCnZoneDownFallback];
        }
        ImGuiEx.Tooltip($"当前内置值：{Strings.OpcodeValues(OpcodeUpdater.KnownCnZoneDownFallback)}");
        ImGui.Separator();
        ImGui.Checkbox("禁用 ZoneUp 自动检测", ref C.DisableZoneUpAutoDetect);
        if(C.DisableZoneUpAutoDetect)
        {
            ImGui.Indent();
            ImGuiEx.TextWrapped($"""
                请输入 ZoneUp opcode。
                获取方法：前往{Strings.InnRoomExample}，输入 /xldata network，然后在不做任何操作的情况下等待片刻。你应该会看到一个 Direction=ZoneUp 且按固定时间间隔重复出现的 opcode，把 OpCode 列中的值填进来。
                """);
            EditOpcodes("##zoneUp", ref C.OpcodesZoneUp);
            ImGui.Unindent();
        }
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Check, Strings.Apply))
        {
            OpcodeUpdater.Save();
        }

        ImGui.Unindent();
    }

    void EditOpcodes(string id, ref uint[] opcodes)
    {
        var str = Strings.OpcodeValues(opcodes);
        List<uint> newOpcodes = [];
        if(ImGui.InputText(id, ref str))
        {
            foreach(var x in str.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if(OpcodeUpdater.TryParseOpcodeInput(x, out var result))
                {
                    newOpcodes.Add(result);
                }
            }
            opcodes = newOpcodes.ToArray();
        }
    }

    void DrawZoneEditor()
    {
        var TerrID = Svc.ClientState.TerritoryType;
    }

    public override void OnClose()
    {
        P.SaveZoneData();
    }

    int[] ints = new int[100];
    uint[] d = new uint[100];
    long[] longs = new long[100];
    void DrawDebug()
    {
        try
        {
            var cs = DalamudReflector.GetService("Dalamud.Game.ClientState.ClientState");
            ImGuiEx.Text($"{cs.GetFoP("TerritoryType")}");
        }
        catch(Exception e)
        {
            e.Log();
        }
        {
            var l = LayoutWorld.Instance()->ActiveLayout;
            if(l != null)
            {
                ImGuiEx.Text($"{l->FestivalStatus:X8}");
                ImGuiEx.Text($"{l->ActiveFestivals[0]}");
                ImGuiEx.Text($"{l->ActiveFestivals[1]}");
                ImGuiEx.Text($"{l->ActiveFestivals[2]}");
                ImGuiEx.Text($"{l->ActiveFestivals[3]}");
                ImGui.InputInt("活动 0", ref ints[0]);
                ImGui.InputInt("活动 1", ref ints[1]);
                ImGui.InputInt("活动 2", ref ints[2]);
                ImGui.InputInt("活动 3", ref ints[3]);
                if (ImGui.Button("设置活动"))
                {
                    var s = stackalloc uint[] { (uint)ints[0], (uint)ints[1], (uint)ints[2], (uint)ints[3] };
                    l->SetActiveFestivals((FFXIVClientStructs.FFXIV.Client.Game.GameMain.Festival*)s);
                }
            }
        }

        ImGui.Checkbox("绕过所有限制", ref P.Bypass);
        if(ImGui.Button("按支持天气填充阶段"))
        {
            if (P.Weathers.TryGetValue(Svc.ClientState.TerritoryType, out var weathers))
            {
                if (!(P.ZoneData.Data.TryGetValue(Utils.GetLayout(), out var level)))
                {
                    level = new()
                    {
                        Name = ExcelTerritoryHelper.GetName(Svc.ClientState.TerritoryType)
                    };
                    P.ZoneData.Data[Utils.GetLayout()] = level;
                }
                var i = 0u;
                level.Phases = [];
                foreach (var x in weathers)
                {
                    level.Phases.Add(new() { Weather = x, Name = $"Phase {++i}" });
                }
                P.SaveZoneData();
                Notify.Info("成功");
            }
            else
            {
                Notify.Error("失败");
            }
        }
        if(ImGui.CollapsingHeader("地图效果"))
        {
            ImGuiEx.TextCopy($"模块：{Utils.GetMapEffectModule()}");
            ImGuiEx.TextCopy($"地址：{(((nint)EventFramework.Instance()) + 344):X16}");
            ImGui.InputInt("1", ref i1);
            ImGui.InputInt("2", ref i2);
            ImGui.InputInt("3", ref i3);
            if (ImGui.Button("执行"))
            {
                MapEffect.Delegate(Utils.GetMapEffectModule(), (uint)i1, (ushort)i2, (ushort)i3);
            }
            if (ImGui.Button("执行 1 到 i1"))
            {
                for (int i = 1; i <= i1; i++)
                {
                    MapEffect.Delegate(Utils.GetMapEffectModule(), (uint)i, (ushort)i2, (ushort)i3);
                }
            }
        }

        if (ImGui.CollapsingHeader("天气"))
        {
            ImGui.InputInt("天气", ref i4);
            if(ImGui.Button("设置天气"))
            {
                var e = EnvManager.Instance();
                e->ActiveWeather = (byte)i4;
                e->TransitionTime = 0.5f;
            }
            var s = (int)*P.Memory.ActiveScene;
            if(ImGui.InputInt("场景", ref s))
            {
                *P.Memory.ActiveScene = (byte)s;
            }
        }

        if (ImGui.CollapsingHeader("监视钩子"))
        {
            if (ImGui.Button("启用钩子")) P.Memory.PacketDispatcher_OnReceivePacketMonitorHook.Enable();
            if (ImGui.Button("暂停钩子")) P.Memory.PacketDispatcher_OnReceivePacketMonitorHook.Pause();
            if (ImGui.Button("禁用钩子")) P.Memory.PacketDispatcher_OnReceivePacketMonitorHook.Disable();
        }

        if (ImGui.CollapsingHeader("Story"))
        {/*
            foreach (var x in Svc.Data.GetExcelSheet<Story>())
            {
                ImGuiEx.Text($"{x.RowId} {ExcelTerritoryHelper.GetName(x.LayerSetTerritoryType0?.Value?.RowId ?? 0, true)}:");
                for (int i = 0; i < x.LayerSet0.Length; i++)
                {
                    ImGuiEx.Text($"  LayerSet0: {i} = {x.LayerSet0[i]}");
                }
            }*/
        }

        if (ImGui.CollapsingHeader("Story1"))
        {/*
            foreach (var x in Svc.Data.GetExcelSheet<Story>())
            {
                ImGuiEx.Text($"{x.RowId} {ExcelTerritoryHelper.GetName(x.LayerSetTerritoryType1?.Value?.RowId ?? 0, true)}:");
                for (int i = 0; i < x.LayerSet1.Length; i++)
                {
                    ImGuiEx.Text($"  LayerSet1: {i} = {x.LayerSet1[i]}");
                }
            }*/
        }

    }
}
