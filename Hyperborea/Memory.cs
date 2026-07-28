using Dalamud;
using Dalamud.Memory;
using ECommons.ExcelServices;
using ECommons.EzHookManager;
using FFXIVClientStructs.FFXIV.Application.Network;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Client.Network;
using Lumina.Excel.Sheets;
using System.CodeDom;
using System.Net.NetworkInformation;
using System.Threading;
using TerraFX.Interop.Windows;
using static FFXIVClientStructs.FFXIV.Client.Network.PacketDispatcher.Delegates;

namespace Hyperborea;
public unsafe class Memory
{
    internal delegate nint LoadZone(nint a1, uint a2, int a3, byte a4, byte a5, byte a6);
    [EzHook("40 55 56 41 54 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24", false)]
    internal EzHook<LoadZone> LoadZoneHook;

    internal EzHook<PacketDispatcher.Delegates.OnReceivePacket> PacketDispatcher_OnReceivePacketHook;
    internal EzHook<PacketDispatcher.Delegates.OnReceivePacket> PacketDispatcher_OnReceivePacketMonitorHook;

    internal delegate byte PacketDispatcher_OnSendPacket(nint a1, nint a2, nint a3, byte a4);
    [EzHook("48 89 5C 24 ?? 48 89 74 24 ?? 4C 89 64 24 ?? 55 41 56 41 57 48 8B EC 48 83 EC 70", false)]
    internal EzHook<PacketDispatcher_OnSendPacket> PacketDispatcher_OnSendPacketHook;

    internal delegate nint TargetSystem_InteractWithObject(nint a1, nint a2, byte a3);
    [EzHook("48 89 5C 24 ?? 48 89 6C 24 ?? 56 48 83 EC 20 48 8B E9 41 0F B6 F0", false)]
    internal EzHook<TargetSystem_InteractWithObject> TargetSystem_InteractWithObjectHook;

    internal delegate nint SetupTerritoryTypeDelegate(void* EventFramework, ushort territoryType);
    internal SetupTerritoryTypeDelegate SetupTerritoryType = EzDelegate.Get<SetupTerritoryTypeDelegate>("48 89 5C 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC ?? 48 8B D9 48 89 6C 24");

    internal delegate nint SetupInstanceContent(nint a1, uint a2, uint a3, uint a4);
    [EzHook("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? E8 ?? ?? ?? ?? 8B 54 24 70 48 8B C8 E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B6 54 24", true)]
    internal EzHook<SetupInstanceContent> SetupInstanceContentHook;

    internal delegate byte FinalizeInstanceContent(nint a1, uint a2);
    [EzHook("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 70 48 8D B1", false)]
    internal EzHook<FinalizeInstanceContent> FinalizeInstanceContentHook;

    internal delegate nint IsFlightProhibited();
    [EzHook("40 53 48 83 EC 20 48 8B 1D ?? ?? ?? ?? 48 85 DB 0F 84 ?? ?? ?? ?? 80 3D", false)]
    internal EzHook<IsFlightProhibited> IsFlightProhibitedHook;

    internal byte* ActiveScene;

    private static ushort HeartbeatOpcode;
    private long lastAllowedZoneUpAt;

    internal delegate byte IsAllowedToReceiveDirectMessages(nint a1, int checkType, byte a3, byte a4);
    [EzHook("48 89 5C 24 ?? 57 48 83 EC 20 48 63 FA 41 0F B6 D8", false)]
    internal EzHook<IsAllowedToReceiveDirectMessages> IsAllowedToReceiveDirectMessagesHook;
    
    public Memory()
    {
        var packetDispatcherAddr = (nint)CSFramework.Instance()->NetworkModuleProxy->NetworkModule->PacketReceiverCallback->PacketDispatcher.VirtualTable->OnReceivePacket;
        PluginLog.Information($"OnReceivePacket: {packetDispatcherAddr}");
        if(packetDispatcherAddr == 0) throw new ArgumentOutOfRangeException(nameof(packetDispatcherAddr));
        PacketDispatcher_OnReceivePacketHook = new(packetDispatcherAddr, PacketDispatcher_OnReceivePacketDetour, false);
        PacketDispatcher_OnReceivePacketMonitorHook = new(packetDispatcherAddr, PacketDispatcher_OnReceivePacketMonitorDetour, false);
        HeartbeatOpcode = (ushort)Marshal.ReadInt32(Svc.SigScanner.ScanText("C7 44 24 ?? ?? ?? ?? ?? 48 F7 F1") + 0x4);
        PluginLog.Information($"ZoneUp opcode: {HeartbeatOpcode}");
        EzSignatureHelper.Initialize(this);
        ActiveScene = (byte*)(((nint)EnvManager.Instance()) + 36);
    }

    internal nint IsFlightProhibitedDetour()
    {
        try
        {
            if (P.Enabled && C.EnableAdvancedUnsafeControls && C.ForcedFlight) return 0;
        }
        catch(Exception e)
        {
            e.Log();
        }
        return IsFlightProhibitedHook.Original();
    }

    private byte FinalizeInstanceContentDetour(nint a1, uint a2)
    {
        PluginLog.Debug($"FinalizeInstanceContentDetour: {a2:X8}");
        return FinalizeInstanceContentHook.Original(a1, a2);
    }

    private nint SetupInstanceContentDetour(nint a1, uint a2, uint a3, uint a4)
    {
        try
        {
            PluginLog.Debug($"SetupInstanceContentDetour: {a2:X8}, {a3}, {a4}");
            var l = LayoutWorld.Instance()->ActiveLayout;
            if (l != null)
            {
                var obj = l->TerritoryTypeId;
                var level = Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(obj)?.Bg.ExtractText();
                if (!level.IsNullOrEmpty())
                {
                    if (Utils.TryGetZoneInfo(level, out var info))
                    {
                        P.SaveZoneData();
                    }
                }
            }
        }
        catch(Exception e)
        {
            e.Log();
        }
        return SetupInstanceContentHook.Original(a1, a2, a3, a4);
    }

    private void PacketDispatcher_OnReceivePacketMonitorDetour(PacketDispatcher* a1, uint a2, nint a3)
    {
        PacketDispatcher_OnReceivePacketMonitorHook.Original(a1, a2, a3);
        try
        {
            var opcode = *(ushort*)(a3 + 2);
            var dataPtr = a3 + 16;
            if(opcode == 0xE2)
            {
                var acopcode = *(ushort*)(dataPtr);
                var data = "";
                try
                {
                    data = $"{MemoryHelper.ReadRaw(dataPtr + 4, 28).Select(x => $"{x:X2}").Print(" ")}";
                }
                catch{ }
                PluginLog.Debug($"ActorControl: {acopcode} / {data}"); //
            }
        }
        catch(Exception e)
        {
            e.Log();
        }
        //return ret;
    }

    private byte IsAllowedToReceiveDirectMessagesDetour(nint a1, int checkType, byte a3, byte a4)
    {
        if(checkType == 2)
        {
            return 1;
        }
        return IsAllowedToReceiveDirectMessagesHook.Original(a1, checkType, a3, a4);
    }

    private nint TargetSystem_InteractWithObjectDetour(nint a1, nint a2, byte a3)
    {
        return 0;
    }

    public void EnableSafetyHooks()
    {
        Interlocked.Exchange(ref lastAllowedZoneUpAt, 0);
        IsAllowedToReceiveDirectMessagesHook.Enable();
        PacketDispatcher_OnReceivePacketHook.Enable();
        PacketDispatcher_OnSendPacketHook.Enable();
        TargetSystem_InteractWithObjectHook.Enable();
        LoadZoneHook.Enable();
        IsFlightProhibitedHook?.Enable();
    }

    public void DisableSafetyHooks()
    {
        IsAllowedToReceiveDirectMessagesHook.Pause();
        PacketDispatcher_OnReceivePacketHook.Pause();
        PacketDispatcher_OnSendPacketHook.Pause();
        TargetSystem_InteractWithObjectHook.Pause();
        LoadZoneHook.Pause();
        IsFlightProhibitedHook?.Pause();
    }

    public void EnableFirewall() => EnableSafetyHooks();

    public void DisableFirewall() => DisableSafetyHooks();

    public bool IsFirewallEnabled => PacketDispatcher_OnSendPacketHook.IsEnabled;

    public bool AreSafetyHooksEnabled =>
        PacketDispatcher_OnReceivePacketHook.IsEnabled
        && PacketDispatcher_OnSendPacketHook.IsEnabled
        && TargetSystem_InteractWithObjectHook.IsEnabled
        && LoadZoneHook.IsEnabled;

    public bool IsPacketConfigurationSane(out string reason)
    {
        if (C.OpcodesZoneDown.Length == 0 || C.OpcodesZoneDown.Any(x => x == 0 || x > ushort.MaxValue))
        {
            reason = "ZoneDown opcode 未设置或超出有效范围。";
            return false;
        }

        if (C.DisableZoneUpAutoDetect)
        {
            if (C.OpcodesZoneUp.Length == 0 || C.OpcodesZoneUp.Any(x => x == 0 || x > ushort.MaxValue))
            {
                reason = "已禁用 ZoneUp 自动检测，但手动 ZoneUp opcode 未设置或无效。";
                return false;
            }
        }
        else if (HeartbeatOpcode == 0 || HeartbeatOpcode == ushort.MaxValue)
        {
            reason = "自动检测到的心跳 opcode 无效。";
            return false;
        }

        reason = "";
        return true;
    }

    private byte PacketDispatcher_OnSendPacketDetour(nint a1, nint a2, nint a3, byte a4)
    {
        const byte DefaultReturnValue = 1;

        if (a2 == IntPtr.Zero)
        {
            PluginLog.Error("[HyperFirewall] Error: Opcode pointer is null.");
            return DefaultReturnValue;
        }

        try
        {
            var opcode = *(ushort*)a2;

            if(C.DisableZoneUpAutoDetect && C.OpcodesZoneUp.Contains((uint)opcode))
            {
                if (!TryConsumeHeartbeatCadence(opcode))
                    return DefaultReturnValue;
                PluginLog.Verbose($"[HyperFirewall] (manual management) Passing outgoing packet with opcode {opcode} through.");
                return PacketDispatcher_OnSendPacketHook.Original(a1, a2, a3, a4);
            }
            else if (!C.DisableZoneUpAutoDetect && opcode == HeartbeatOpcode)
            {
                if (!TryConsumeHeartbeatCadence(opcode))
                    return DefaultReturnValue;
                PluginLog.Verbose($"[HyperFirewall] Passing outgoing packet with opcode {opcode} through.");
                return PacketDispatcher_OnSendPacketHook.Original(a1, a2, a3, a4);
            }
            else
            {
                PluginLog.Verbose($"[HyperFirewall] Suppressing outgoing packet with opcode {opcode}.");
            }
        }
        catch (Exception e)
        {
            PluginLog.Error($"[HyperFirewall] Exception caught while processing opcode: {e.Message}");
            e.Log();
            return DefaultReturnValue;
        }

        return DefaultReturnValue;
    }

    private bool TryConsumeHeartbeatCadence(ushort opcode)
    {
        const long minimumIntervalMs = 250;
        var now = Environment.TickCount64;
        var previous = Interlocked.Read(ref lastAllowedZoneUpAt);
        if (previous != 0 && now - previous < minimumIntervalMs)
        {
            PluginLog.Warning($"[HyperFirewall] Suppressing opcode {opcode}: repeated sooner than {minimumIntervalMs} ms.");
            return false;
        }

        Interlocked.Exchange(ref lastAllowedZoneUpAt, now);
        return true;
    }

    private void PacketDispatcher_OnReceivePacketDetour(PacketDispatcher* a1, uint a2, nint a3)
    {
        if (a3 == IntPtr.Zero)
        {
            PluginLog.Error("[HyperFirewall] Error: Data pointer is null.");
            return;
        }

        try
        {
            var opcode = *(ushort*)(a3 + 2);

            if (C.OpcodesZoneDown.Contains(opcode))
            {
                PluginLog.Verbose($"[HyperFirewall] Passing incoming packet with opcode {opcode} through.");
                PacketDispatcher_OnReceivePacketHook.Original(a1, a2, a3);
            }
            else
            {
                PluginLog.Verbose($"[HyperFirewall] Suppressing incoming packet with opcode {opcode}.");
            }
        }
        catch (Exception e)
        {
            PluginLog.Error($"[HyperFirewall] Exception caught while processing opcode: {e.Message}");
            e.Log();
            return;
        }

        return;
    }

    internal nint LoadZoneDetour(nint a1, uint a2, int a3, byte a4, byte a5, byte a6)
    {
        if (P?.Session?.IsActive == true && !P.Session.IsTrustedZoneLoad)
        {
            PluginLog.Warning($"[HyperSafety] Blocked an untrusted local zone transition to {a2}.");
            P.Session.NotifyBlockedZoneTransition(a2);
            return 0;
        }

        try
        {
            PluginLog.Debug($"Loading {ExcelTerritoryHelper.GetName(a2, true)}, {a3}, {a4}, {a5}, {a6}");
        }
        catch(Exception e)
        {
            e.Log();
        }
        return LoadZoneHook.Original(a1, a2, a3, a4, a5, a6);
    }
}
