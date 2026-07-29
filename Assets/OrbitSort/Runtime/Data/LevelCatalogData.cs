using System;

namespace OrbitSort.Data
{
    [Serializable]
    public sealed class LevelCatalogData
    {
        public int schemaVersion = 1;
        public string catalogId = string.Empty;
        public PrototypeRulesData rules = new PrototypeRulesData();
        public LevelData[] levels = Array.Empty<LevelData>();
    }

    [Serializable]
    public sealed class PrototypeRulesData
    {
        public bool timerEnabled;
        public bool moveCounterEnabled;
        public bool ringRequiresGapToRotate = true;
        public bool gatesAreOneWay = true;
        public bool deadlockWhenNoProgress = true;
    }

    [Serializable]
    public sealed class LevelData
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string designerNotes = string.Empty;
        public RingData[] rings = Array.Empty<RingData>();
        public GateData[] gates = Array.Empty<GateData>();
        public ExitData[] exits = Array.Empty<ExitData>();
        public string[] tutorialSteps = Array.Empty<string>();
    }

    [Serializable]
    public sealed class RingData
    {
        public string id = string.Empty;
        public int capacity;
        public int rotationOffset;
        public MarbleData[] marbles = Array.Empty<MarbleData>();
    }

    [Serializable]
    public sealed class MarbleData
    {
        public int index;
        public string color = string.Empty;
    }

    [Serializable]
    public sealed class GateData
    {
        public string id = string.Empty;
        public string fromRing = string.Empty;
        public string toRing = string.Empty;
        public int fromIndex;
        public int toIndex;
        public string direction = "outward";
    }

    [Serializable]
    public sealed class ExitData
    {
        public string id = string.Empty;
        public string ring = string.Empty;
        public int ringIndex;
        public string color = string.Empty;
    }
}
