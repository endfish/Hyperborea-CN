using Dalamud.Plugin.Ipc.Exceptions;
using ECommons.Configuration;
using System.Globalization;
using System.IO;
using System.Net.Http;

namespace Hyperborea.Services.OpcodeUpdaterService;
public unsafe class OpcodeUpdater : IDisposable
{
    volatile bool Disposed = false;
    public static string CurrentVersion => $"{CSFramework.Instance()->GameVersionString}_{P.GetType().Assembly.GetName().Version}";
    public static readonly uint[] KnownCnZoneDownFallback = [0x3C9, 0x2D8];

    private OpcodeUpdater()
    {
        if (CurrentVersion == C.GameVersion && C.OpcodesZoneDown.Any())
        {
            PluginLog.Information("No opcode update required");
            P.AllowedOperation = true;
        }
        else
        {
            PluginLog.Information("New game version detected, opcode update required");
            RunForCurrentVersion();
        }
    }

    public void RunForCurrentVersion()
    {
        var v = CSFramework.Instance()->GameVersionString;
        if (!C.ManualOpcodeManagement)
        {
            S.ThreadPool.Run(() => UpdateOpcodes(v));
        }
    }

    private void UpdateOpcodes(string gameVersion)
    {
        try
        {
            if (Disposed) throw new Exception("Opcode updater was disposed");

            if (TryLoadRemoteOpcodes(gameVersion, out var remoteOpcodes))
            {
                ApplyOpcodes(remoteOpcodes, true, $"downloaded opcode file for {gameVersion}");
                return;
            }

            if (TryLoadBundledOpcodes(gameVersion, out var bundledOpcodes))
            {
                ApplyOpcodes(bundledOpcodes, true, $"bundled opcode file for {gameVersion}");
                return;
            }

            if (TryLoadLatestBundledOpcodes(out var latestBundledOpcodes, out var latestVersion))
            {
                ApplyOpcodes(latestBundledOpcodes, false, $"latest bundled opcode file ({latestVersion})");
                return;
            }

            ApplyOpcodes(new OpcodeData { ZoneDown = [.. KnownCnZoneDownFallback] }, false, "built-in CN fallback");
        }
        catch (Exception ex)
        {
            PluginLog.Warning("Failed to resolve opcodes for current game version");
            ex.LogWarning();
        }
    }

    public static void Save(bool markCurrentVersion = true)
    {
        C.GameVersion = markCurrentVersion ? CurrentVersion : "";
        P.AllowedOperation = true;
        EzConfig.Save();
        PluginLog.Information("New opcodes received. Plugin operational.");
    }

    public static bool TryParseOpcodeInput(string input, out uint opcode)
    {
        input = input.Trim();
        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return uint.TryParse(input[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out opcode);
        }
        if (uint.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out opcode))
        {
            return true;
        }
        return uint.TryParse(input, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out opcode);
    }

    void ApplyOpcodes(OpcodeData data, bool markCurrentVersion, string source)
    {
        if (!data.ZoneDown.Any(x => x != 0))
        {
            throw new Exception("No ZoneDown opcodes were parsed");
        }

        PluginLog.Information($"Using {source}. ZoneDown: {Strings.OpcodeValues(data.ZoneDown)}");
        Svc.Framework.RunOnFrameworkThread(() =>
        {
            C.OpcodesZoneDown = data.ZoneDown;
            if (data.ZoneUp.Length > 0)
            {
                C.OpcodesZoneUp = data.ZoneUp;
            }
            Save(markCurrentVersion);
        });
    }

    static bool TryLoadRemoteOpcodes(string gameVersion, out OpcodeData data)
    {
        using var client = new HttpClient();
        try
        {
            var result = client.GetStringAsync($"https://github.com/kawaii/Hyperborea/raw/main/opcodes/{gameVersion}.txt").Result;
            return TryParseOpcodeData(result, out data);
        }
        catch (Exception ex)
        {
            PluginLog.Warning($"Failed to download opcodes for {gameVersion}");
            ex.LogWarning();
            data = default;
            return false;
        }
    }

    static bool TryLoadBundledOpcodes(string gameVersion, out OpcodeData data)
    {
        var path = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "opcodes", $"{gameVersion}.txt");
        if (File.Exists(path) && TryParseOpcodeData(File.ReadAllText(path), out data))
        {
            return true;
        }
        data = default;
        return false;
    }

    static bool TryLoadLatestBundledOpcodes(out OpcodeData data, out string version)
    {
        var dir = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "opcodes");
        if (Directory.Exists(dir))
        {
            foreach (var path in Directory.EnumerateFiles(dir, "*.txt").OrderByDescending(Path.GetFileNameWithoutExtension, StringComparer.Ordinal))
            {
                if (TryParseOpcodeData(File.ReadAllText(path), out data))
                {
                    version = Path.GetFileNameWithoutExtension(path);
                    return true;
                }
            }
        }

        data = default;
        version = "";
        return false;
    }

    static bool TryParseOpcodeData(string content, out OpcodeData data)
    {
        uint[] zoneDown = [];
        uint[] zoneUp = [];

        foreach (var line in content.ReplaceLineEndings().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("ZoneUp=", StringComparison.OrdinalIgnoreCase))
            {
                zoneUp = ParseOpcodeList(line["ZoneUp=".Length..]);
            }
            else if (line.StartsWith("ZoneDown=", StringComparison.OrdinalIgnoreCase))
            {
                zoneDown = ParseOpcodeList(line["ZoneDown=".Length..]);
            }
        }

        data = new OpcodeData
        {
            ZoneDown = zoneDown,
            ZoneUp = zoneUp,
        };
        return zoneDown.Any(x => x != 0);
    }

    static uint[] ParseOpcodeList(string value)
    {
        List<uint> opcodes = [];
        foreach (var s in value.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryParseOpcodeInput(s, out var opcode))
            {
                opcodes.Add(opcode);
            }
        }
        return [.. opcodes];
    }

    readonly struct OpcodeData
    {
        public uint[] ZoneDown { get; init; }
        public uint[] ZoneUp { get; init; }
    }

    public void Dispose()
    {
        Disposed = true;
    }
}
