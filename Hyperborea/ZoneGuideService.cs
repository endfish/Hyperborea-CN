using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using System.Text.RegularExpressions;
using static Lumina.Data.Parsing.Layer.LayerCommon;

namespace Hyperborea;

public sealed class GuidePoint
{
    public string Name = "";
    public Point3 Position = new();
    public uint InstanceId;
    public uint LayerId;
    public string LayerName = "";
    public string Kind = "";
    internal int SortOrder;
}

public sealed partial class ZoneGuideService
{
    private readonly Dictionary<string, IReadOnlyList<GuidePoint>> cache = [];

    [GeneratedRegex(@"(?:^|_)phase_?(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PhaseRegex();

    [GeneratedRegex(@"(?:^|_)boss_?(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex BossRegex();

    public IReadOnlyList<GuidePoint> GetGuidePoints(string bg)
    {
        if (bg.IsNullOrEmpty())
            return [];

        if (cache.TryGetValue(bg, out var cached))
            return cached;

        cached = BuildGuidePoints(bg);
        cache[bg] = cached;
        return cached;
    }

    public void ClearCache() => cache.Clear();

    private static IReadOnlyList<GuidePoint> BuildGuidePoints(string bg)
    {
        try
        {
            var slash = bg.LastIndexOf('/');
            var dir = slash >= 0 ? bg[..slash] : bg;
            var lgb = Svc.Data.GetFile<LgbFile>($"bg/{dir}/planmap.lgb");
            if (lgb == null)
                return [];

            var candidates = new List<GuidePoint>();
            foreach (var layer in lgb.Layers)
            {
                var layerName = layer.Name ?? "";
                var phase = ParseNumber(PhaseRegex(), layerName);
                var boss = ParseNumber(BossRegex(), layerName);

                foreach (var instance in layer.InstanceObjects)
                {
                    if (instance.AssetType != LayerEntryType.PopRange
                        || instance.Object is not PopRangeInstanceObject pop
                        || pop.PopType != PopType.PC)
                        continue;

                    var p = instance.Transform.Translation;
                    candidates.Add(new GuidePoint
                    {
                        Name = BuildPopRangeName(layerName, phase, boss),
                        Position = new(p.X, p.Y, p.Z),
                        InstanceId = instance.InstanceId,
                        LayerId = layer.LayerId,
                        LayerName = layerName,
                        Kind = "PopRange",
                        SortOrder = BuildSortOrder(phase, boss, layerName, true),
                    });
                }

            }

            var result = new List<GuidePoint>();
            foreach (var candidate in candidates
                         .OrderBy(x => x.SortOrder)
                         .ThenBy(x => x.LayerId)
                         .ThenBy(x => x.InstanceId))
            {
                var isGenericZonePoint = candidate.LayerName.Contains("zone", StringComparison.OrdinalIgnoreCase);
                var duplicateRadiusSquared = isGenericZonePoint ? 35f * 35f : 4f * 4f;
                if (result.Any(x => Vector3.DistanceSquared(x.Position.ToVector3(), candidate.Position.ToVector3()) < duplicateRadiusSquared))
                    continue;

                var duplicateNameCount = result.Count(x => x.Name == candidate.Name);
                if (duplicateNameCount > 0)
                    candidate.Name += $" #{duplicateNameCount + 1}";
                result.Add(candidate);
            }

            return result;
        }
        catch (Exception e)
        {
            PluginLog.Error($"Failed to build guide points for {bg}.\n{e}");
            return [];
        }
    }

    private static int? ParseNumber(Regex regex, string value)
    {
        var match = regex.Match(value);
        return match.Success && int.TryParse(match.Groups[1].Value, out var number) ? number : null;
    }

    private static string BuildPopRangeName(string layerName, int? phase, int? boss)
    {
        if (boss is not null)
            return $"首领 {boss} · 入场点";
        if (phase is not null)
            return layerName.Contains("gimmick", StringComparison.OrdinalIgnoreCase)
                ? $"阶段 {phase} · 转场点"
                : $"阶段 {phase} · 出生点";
        if (layerName.Contains("zone", StringComparison.OrdinalIgnoreCase))
            return "区域导航点";
        return $"{NormalizeLayerName(layerName)} · 出生点";
    }

    private static int BuildSortOrder(int? phase, int? boss, string layerName, bool isPopRange)
    {
        if (boss is not null)
            return boss.Value * 100 + 60;
        if (phase is not null)
            return phase.Value * 100 + (isPopRange ? 20 : 10);
        if (layerName.Contains("zone", StringComparison.OrdinalIgnoreCase))
            return 9_000;
        return 10_000;
    }

    private static string NormalizeLayerName(string layerName)
    {
        var value = layerName.Trim();
        if (value.StartsWith("LVD_", StringComparison.OrdinalIgnoreCase))
            value = value[4..];
        value = value.Replace('_', ' ').Trim();
        return value.IsNullOrEmpty() ? "未命名区域" : value;
    }
}
