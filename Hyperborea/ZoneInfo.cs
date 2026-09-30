namespace Hyperborea;
[Serializable]
public class ZoneInfo
{
    [NonSerialized] internal string GUID = Guid.NewGuid().ToString();
    public string Name = "";
    public Point3 Spawn;
    public List<PhaseInfo> Phases = [];
}

[Serializable]
public class PhaseInfo
{
    [NonSerialized] internal string GUID = Guid.NewGuid().ToString();
    public string Name = "";
    public uint Weather = 0;
    public List<MapEffectInfo> MapEffects = [];
    public List<FestivalInfo> Festivals = [];
}

[Serializable]
public class MapEffectInfo
{
    [NonSerialized] internal string GUID = Guid.NewGuid().ToString();
    public int Slot;
    public int State;
    public int TimelineOverride;

    // Accept existing overrides and clipboard data; new saves use the named fields above.
    [YamlDotNet.Serialization.YamlMember(Alias = "a1"), Newtonsoft.Json.JsonIgnore]
    public int LegacySlot { get => Slot; set => Slot = value; }

    [YamlDotNet.Serialization.YamlMember(Alias = "a2"), Newtonsoft.Json.JsonIgnore]
    public int LegacyState { get => State; set => State = value; }

    [YamlDotNet.Serialization.YamlMember(Alias = "a3"), Newtonsoft.Json.JsonIgnore]
    public int LegacyTimelineOverride { get => TimelineOverride; set => TimelineOverride = value; }
}

[Serializable]
public class FestivalInfo
{
    [NonSerialized] internal string GUID = Guid.NewGuid().ToString();
    public int Id;
    public int PhaseId = -1;
}

[Serializable]
public class DirectorInitInfo
{
    public uint a1;
    public uint a2;
}
