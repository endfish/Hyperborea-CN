using Dalamud.Game.ClientState.Keys;
using ECommons.Configuration;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using Hyperborea.Gui;

namespace Hyperborea;

public enum ExplorationState
{
    Disabled,
    Arming,
    Exploring,
    Reverting,
    Faulted,
}

public unsafe sealed class ExplorationSession
{
    private const int ArmingStableFramesRequired = 3;
    private const int LayoutStableFramesRequired = 20;
    private const long ZoneReadyTimeoutMs = 20_000;
    private const float CoordinateLimit = 100_000f;

    private bool disableAfterRevert;
    private bool revertAfterArming;
    private bool preserveRecoveryOnArmingFailure;
    private bool trustedZoneLoad;
    private int stableFrames;
    private long stateStartedAt;
    private long nextRevertRetryAt;
    private PendingZoneAction pendingZoneAction;

    public ExplorationState State { get; private set; } = ExplorationState.Disabled;
    public string Status { get; private set; } = "未启用";
    public bool IsActive => State != ExplorationState.Disabled;
    public bool IsTrustedZoneLoad => trustedZoneLoad;
    public bool CanNavigate => State == ExplorationState.Exploring && P.Memory.AreSafetyHooksEnabled;
    public bool HasRecoveryNotice => C.RecoveryRequired;

    public bool TryArm(out string error)
    {
        error = "";
        if (State != ExplorationState.Disabled)
        {
            error = "探索会话已经启用。";
            return false;
        }

        if (C.RecoveryRequired)
        {
            error = "检测到上次会话没有完成安全停用。请先确认角色实际位于旅馆，并清除恢复标记。";
            return false;
        }

        if (!Utils.CanEnablePlugin(out var reasons))
        {
            error = reasons.Count == 0 ? "当前状态不允许启用。" : string.Join("、", reasons);
            return false;
        }

        if (!P.Memory.IsPacketConfigurationSane(out error))
            return false;

        var layout = LayoutWorld.Instance()->ActiveLayout;
        if (!Player.Available || layout == null || layout->InitState != 7)
        {
            error = "当前区域尚未完全加载，无法建立安全基线。";
            return false;
        }

        var position = Player.Object.Position;
        if (!ValidatePosition(position, out error))
            return false;

        UI.SavedPos = position;
        UI.SavedZoneState = new SavedZoneState(layout->TerritoryTypeId, position);
        C.RecoveryRequired = true;
        C.RecoveryTerritory = layout->TerritoryTypeId;
        C.RecoveryPosition = position.ToPoint3();
        EzConfig.Save();

        State = ExplorationState.Arming;
        P.Enabled = true;
        revertAfterArming = false;
        preserveRecoveryOnArmingFailure = false;
        stableFrames = 0;
        stateStartedAt = Environment.TickCount64;
        Status = "正在启用安全钩子…";

        try
        {
            P.Memory.EnableSafetyHooks();
        }
        catch (Exception e)
        {
            PluginLog.Error($"[HyperSafety] Failed to arm safety hooks.\n{e}");
            RollBackArming();
            error = "安全钩子启用失败，探索会话没有启动。";
            return false;
        }

        if (!P.Memory.AreSafetyHooksEnabled)
        {
            RollBackArming();
            error = "并非所有安全钩子都已成功启用。";
            return false;
        }

        return true;
    }

    public bool TryRecoverPreviousSession(out string error)
    {
        error = "";
        if (State != ExplorationState.Disabled || !C.RecoveryRequired)
        {
            error = "当前没有可恢复的异常会话。";
            return false;
        }

        if (!P.Memory.IsPacketConfigurationSane(out error))
            return false;
        if (!Player.Available || LayoutWorld.Instance()->ActiveLayout == null)
        {
            error = "本地玩家或 Layout 尚未就绪。";
            return false;
        }
        if (C.RecoveryTerritory == 0 || !ValidatePosition(C.RecoveryPosition.ToVector3(), out error))
        {
            error = "恢复记录中的原区域或坐标无效。";
            return false;
        }

        UI.SavedPos = C.RecoveryPosition.ToVector3();
        UI.SavedZoneState = new SavedZoneState(C.RecoveryTerritory, C.RecoveryPosition.ToVector3());
        State = ExplorationState.Arming;
        P.Enabled = true;
        stableFrames = 0;
        stateStartedAt = Environment.TickCount64;
        revertAfterArming = true;
        preserveRecoveryOnArmingFailure = true;
        Status = "正在为异常会话重新启用安全钩子…";

        try
        {
            P.Memory.EnableSafetyHooks();
        }
        catch (Exception e)
        {
            PluginLog.Error($"[HyperSafety] Failed to arm recovery hooks.\n{e}");
            RollBackArming();
            error = "安全钩子启用失败；恢复标记已保留。";
            return false;
        }

        if (!P.Memory.AreSafetyHooksEnabled)
        {
            RollBackArming();
            error = "并非所有安全钩子都已成功启用；恢复标记已保留。";
            return false;
        }

        return true;
    }

    public void Tick()
    {
        if (State == ExplorationState.Disabled)
            return;

        if (!P.Memory.AreSafetyHooksEnabled)
        {
            try
            {
                P.Memory.EnableSafetyHooks();
            }
            catch (Exception e)
            {
                PluginLog.Error($"[HyperSafety] Failed to recover safety hooks.\n{e}");
            }

            if (!P.Memory.AreSafetyHooksEnabled)
            {
                EnterFaulted("关键安全钩子失效；已冻结插件移动功能。请不要执行任何游戏操作。");
                return;
            }

            if (State == ExplorationState.Faulted)
            {
                Status = "安全钩子已恢复，正在强制还原。";
                BeginRevert(true);
            }
        }

        switch (State)
        {
            case ExplorationState.Arming:
                TickArming();
                break;
            case ExplorationState.Exploring:
                TickPendingZoneAction();
                break;
            case ExplorationState.Reverting:
                SuppressMovementInput();
                TickReverting();
                break;
            case ExplorationState.Faulted:
                SuppressMovementInput();
                break;
        }
    }

    public void RequestRevert(bool disableWhenComplete)
    {
        if (State == ExplorationState.Disabled)
            return;

        disableAfterRevert |= disableWhenComplete;
        if (State == ExplorationState.Reverting)
            return;

        BeginRevert(disableAfterRevert);
    }

    public void QueueZoneReadyAction(uint territory, Vector3? position, PhaseInfo phase)
    {
        pendingZoneAction = new PendingZoneAction(territory, position, phase, Environment.TickCount64);
        Status = "区域正在加载；安全坐标将在 Layout 就绪后应用。";
    }

    public T ExecuteTrustedZoneLoad<T>(Func<T> action)
    {
        trustedZoneLoad = true;
        try
        {
            return action();
        }
        finally
        {
            trustedZoneLoad = false;
        }
    }

    public bool TryTeleport(Vector3 position, out string error)
    {
        if (!CanNavigate)
        {
            error = "安全会话或关键钩子尚未就绪。";
            return false;
        }

        var layout = LayoutWorld.Instance()->ActiveLayout;
        if (!Player.Available
            || layout == null
            || layout->InitState != 7
            || layout->TerritoryTypeId != Svc.ClientState.TerritoryType)
        {
            error = "本地玩家或当前 Layout 尚未完全就绪。";
            return false;
        }

        if (!ValidatePosition(position, out error))
            return false;

        Player.GameObject->SetPosition(position.X, position.Y, position.Z);
        error = "";
        return true;
    }

    public bool ValidatePosition(Vector3 position, out string error)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
        {
            error = "坐标包含 NaN 或 Infinity。";
            return false;
        }

        if (MathF.Abs(position.X) > CoordinateLimit
            || MathF.Abs(position.Y) > CoordinateLimit
            || MathF.Abs(position.Z) > CoordinateLimit)
        {
            error = $"坐标超出安全限制（±{CoordinateLimit:N0}）。";
            return false;
        }

        error = "";
        return true;
    }

    public void NotifyBlockedZoneTransition(uint territory)
    {
        Status = $"已阻止非插件发起的区域切换（Territory {territory}）。";
    }

    public void Report(string message)
    {
        Status = message;
    }

    public void HandleLogout()
    {
        var wasActive = IsActive;
        pendingZoneAction = null;
        P.Noclip = false;
        C.FastTeleport = false;

        try
        {
            P.Memory.DisableSafetyHooks();
        }
        catch (Exception e)
        {
            PluginLog.Error($"[HyperSafety] Failed to disable hooks after logout.\n{e}");
        }

        State = ExplorationState.Disabled;
        P.Enabled = false;
        UI.SavedPos = null;
        UI.SavedZoneState = null;
        Status = wasActive ? "检测到断线；安全会话已终止，恢复标记已保留。" : "未启用";
    }

    public void PrepareForDispose()
    {
        if (!IsActive)
            return;

        P.Noclip = false;
        C.FastTeleport = false;
        pendingZoneAction = null;

        var saved = UI.SavedZoneState;
        if (saved == null)
            return;

        TrySetPositionInternal(saved.Position, false);
        try
        {
            var layout = LayoutWorld.Instance()->ActiveLayout;
            if (layout == null || layout->TerritoryTypeId != saved.ZoneId)
            {
                ExecuteTrustedZoneLoad(() =>
                {
                    Utils.LoadZone(saved.ZoneId, false, false);
                    return 0;
                });
            }
        }
        catch (Exception e)
        {
            PluginLog.Error($"[HyperSafety] Best-effort dispose recovery failed.\n{e}");
        }
    }

    public void ClearRecoveryNotice()
    {
        if (IsActive)
            return;

        var layout = LayoutWorld.Instance()->ActiveLayout;
        if (!Player.Available
            || layout == null
            || layout->InitState != 7
            || layout->TerritoryTypeId != C.RecoveryTerritory
            || Svc.ClientState.TerritoryType != C.RecoveryTerritory)
        {
            Status = "当前客户端区域与恢复记录不一致，拒绝清除。请先执行异常会话恢复。";
            return;
        }

        C.RecoveryRequired = false;
        C.RecoveryTerritory = 0;
        C.RecoveryPosition = new();
        EzConfig.Save();
        Status = "恢复标记已清除。";
    }

    private void TickArming()
    {
        if (++stableFrames < ArmingStableFramesRequired)
            return;

        if (revertAfterArming)
        {
            revertAfterArming = false;
            Status = "安全钩子已恢复，正在返回记录的原区域。";
            BeginRevert(true);
        }
        else
        {
            State = ExplorationState.Exploring;
            Status = "安全会话已就绪。";
        }
    }

    private void BeginRevert(bool disableWhenComplete)
    {
        var saved = UI.SavedZoneState;
        if (saved == null)
        {
            EnterFaulted("缺少原始区域安全基线，无法自动还原。过滤器将保持开启。");
            return;
        }

        disableAfterRevert = disableWhenComplete;
        pendingZoneAction = null;
        P.TaskManager.Abort();
        P.Noclip = false;
        C.FastTeleport = false;
        State = ExplorationState.Reverting;
        stableFrames = 0;
        stateStartedAt = Environment.TickCount64;
        nextRevertRetryAt = 0;
        Status = "正在安全还原；数据包过滤保持开启。";

        if (Svc.Condition[ConditionFlag.Mounted])
            Player.Character->Mount.CreateAndSetupMount(0, 0, 0, 0, 0, 0, 0);

        TrySetPositionInternal(saved.Position, false);
        TryStartRevertZoneLoad(saved);
    }

    private void TickReverting()
    {
        var saved = UI.SavedZoneState;
        if (saved == null)
        {
            EnterFaulted("还原过程中安全基线丢失。过滤器将保持开启。");
            return;
        }

        var layout = LayoutWorld.Instance()->ActiveLayout;
        var layoutReady = layout != null
            && layout->TerritoryTypeId == saved.ZoneId
            && layout->InitState == 7
            && Svc.ClientState.TerritoryType == saved.ZoneId;

        if (!layoutReady)
        {
            stableFrames = 0;
            if (Environment.TickCount64 >= nextRevertRetryAt)
                TryStartRevertZoneLoad(saved);

            if (Environment.TickCount64 - stateStartedAt > ZoneReadyTimeoutMs)
                Status = "还原尚未完成；过滤器仍保持开启。请等待或再次点击还原。";
            return;
        }

        TrySetPositionInternal(saved.Position, true);
        var positionStable = Player.Available && Vector3.DistanceSquared(Player.Object.Position, saved.Position) < 0.25f;
        var transitioning = Svc.Condition[ConditionFlag.BetweenAreas] || Svc.Condition[ConditionFlag.BetweenAreas51];
        stableFrames = positionStable && !transitioning ? stableFrames + 1 : 0;

        if (stableFrames < LayoutStableFramesRequired)
        {
            Status = $"原区域已加载，正在确认稳定状态（{stableFrames}/{LayoutStableFramesRequired}）…";
            return;
        }

        CompleteRevert();
    }

    private void CompleteRevert()
    {
        pendingZoneAction = null;
        stableFrames = 0;

        if (!disableAfterRevert)
        {
            State = ExplorationState.Exploring;
            Status = "已安全还原到原区域；过滤器仍在运行。";
            return;
        }

        try
        {
            P.Memory.DisableSafetyHooks();
        }
        catch (Exception e)
        {
            PluginLog.Error($"[HyperSafety] Failed to disarm safety hooks.\n{e}");
            EnterFaulted("还原完成，但安全钩子未能正常关闭。");
            return;
        }

        State = ExplorationState.Disabled;
        P.Enabled = false;
        disableAfterRevert = false;
        UI.SavedPos = null;
        UI.SavedZoneState = null;
        C.RecoveryRequired = false;
        C.RecoveryTerritory = 0;
        C.RecoveryPosition = new();
        EzConfig.Save();
        Status = "已安全停用。";
    }

    private void TickPendingZoneAction()
    {
        var pending = pendingZoneAction;
        if (pending == null)
            return;

        if (Environment.TickCount64 - pending.StartedAt > ZoneReadyTimeoutMs)
        {
            pendingZoneAction = null;
            Status = "区域加载超时；未应用坐标或阶段。过滤器仍保持开启。";
            return;
        }

        var layout = LayoutWorld.Instance()->ActiveLayout;
        if (layout == null || layout->TerritoryTypeId != pending.Territory || layout->InitState != 7)
        {
            pending.StableFrames = 0;
            return;
        }

        if (++pending.StableFrames < 5)
            return;

        if (pending.Position is { } position && !TryTeleport(position, out var error))
        {
            pendingZoneAction = null;
            Status = $"区域已加载，但安全坐标未应用：{error}";
            return;
        }

        try
        {
            pending.Phase?.SwitchTo();
        }
        catch (Exception e)
        {
            PluginLog.Error($"[HyperSafety] Failed to apply initial visual phase.\n{e}");
            Status = "区域和坐标已加载，但初始视觉阶段应用失败。";
            pendingZoneAction = null;
            return;
        }

        pendingZoneAction = null;
        Status = "区域已安全加载。";
    }

    private void TryStartRevertZoneLoad(SavedZoneState saved)
    {
        nextRevertRetryAt = Environment.TickCount64 + 2_000;
        try
        {
            var layout = LayoutWorld.Instance()->ActiveLayout;
            if (layout != null && layout->TerritoryTypeId == saved.ZoneId && layout->InitState == 7)
                return;

            ExecuteTrustedZoneLoad(() =>
            {
                Utils.LoadZone(saved.ZoneId, false, false);
                return 0;
            });
        }
        catch (Exception e)
        {
            PluginLog.Error($"[HyperSafety] Failed to start revert zone load.\n{e}");
            Status = "重新加载原区域失败；过滤器仍保持开启，稍后会重试。";
        }
    }

    private bool TrySetPositionInternal(Vector3 position, bool requireLayoutReady)
    {
        if (!Player.Available || !ValidatePosition(position, out _))
            return false;

        if (requireLayoutReady)
        {
            var layout = LayoutWorld.Instance()->ActiveLayout;
            if (layout == null || layout->InitState != 7)
                return false;
        }

        Player.GameObject->SetPosition(position.X, position.Y, position.Z);
        return true;
    }

    private void EnterFaulted(string message)
    {
        State = ExplorationState.Faulted;
        P.Enabled = true;
        P.Noclip = false;
        C.FastTeleport = false;
        pendingZoneAction = null;
        Status = message;

        if (UI.SavedZoneState is { } saved)
            TrySetPositionInternal(saved.Position, false);
    }

    private void RollBackArming()
    {
        try
        {
            P.Memory.DisableSafetyHooks();
        }
        catch (Exception e)
        {
            PluginLog.Error($"[HyperSafety] Failed to roll back safety hooks.\n{e}");
        }

        State = ExplorationState.Disabled;
        P.Enabled = false;
        revertAfterArming = false;
        UI.SavedPos = null;
        UI.SavedZoneState = null;
        if (!preserveRecoveryOnArmingFailure)
        {
            C.RecoveryRequired = false;
            C.RecoveryTerritory = 0;
            C.RecoveryPosition = new();
        }
        preserveRecoveryOnArmingFailure = false;
        EzConfig.Save();
        Status = "安全会话启动失败。";
    }

    private static void SuppressMovementInput()
    {
        foreach (var key in new[]
                 {
                     VirtualKey.W,
                     VirtualKey.A,
                     VirtualKey.S,
                     VirtualKey.D,
                     VirtualKey.SPACE,
                     VirtualKey.LSHIFT,
                     VirtualKey.RSHIFT,
                 })
        {
            Svc.KeyState.SetRawValue(key, 0);
        }
    }

    private sealed class PendingZoneAction(uint territory, Vector3? position, PhaseInfo phase, long startedAt)
    {
        public uint Territory { get; } = territory;
        public Vector3? Position { get; } = position;
        public PhaseInfo Phase { get; } = phase;
        public long StartedAt { get; } = startedAt;
        public int StableFrames { get; set; }
    }
}
