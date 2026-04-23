using System.Collections.Generic;
using System.Linq;

namespace Hyperborea;

internal static class Strings
{
    public const string PluginName = "HyperboreaCN";
    public const string Command = "/hyper";

    public const string SettingsWindowTitle = PluginName + " 设置";
    public const string LogWindowTitle = PluginName + " 日志";
    public const string EditorWindowTitle = PluginName + " 区域编辑器";
    public const string DebugWindowTitle = PluginName + " 调试";
    public const string OverlayWindowTitle = PluginName + " 覆盖层";
    public const string CompassWindowTitle = PluginName + " 指南针";

    public const string SelectMount = "选择坐骑...";
    public const string Filter = "筛选";
    public const string Search = "搜索";
    public const string NoMount = "不使用坐骑";
    public const string SelectPhase = "选择阶段";
    public const string SelectFestivals = "选择活动...";
    public const string DeselectAll = "全部取消";
    public const string Apply = "应用";
    public const string Save = "保存";
    public const string Reset = "重置";
    public const string Delete = "删除";
    public const string Browse = "浏览";
    public const string CurrentZone = "当前区域";
    public const string ZoneEditor = "区域编辑器";
    public const string LoadZone = "加载区域";
    public const string Revert = "还原";
    public const string NotDefined = "未定义";
    public const string InnRoomExample = "旅馆房间（例如栖木旅馆）";

    public static string UnknownFestival(int id) => $"未知活动 {id}";
    public static string OpcodeUpdateError(string message) => $"更新 opcode 失败：\n{message}";
    public static string RestrictedConditions(IEnumerable<string> reasons) => $"当前无法启用 {PluginName}，原因如下：\n{string.Join("\n", reasons)}";
    public static string EditingZone(string zoneName) => $"当前编辑：{zoneName}";
    public static string SharedDataWith(IEnumerable<string> zones) => $"与以下区域共享数据：\n{string.Join("\n", zones)}";
    public static string OpcodeValues(IEnumerable<uint> values) => string.Join(", ", values.Select(x => $"0x{x:X}"));
}
