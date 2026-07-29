using System;
using System.Collections.Generic;
using System.Linq;
using OrbitSort.Data;

namespace OrbitSort.Core
{
    public enum MarbleColor
    {
        Blue,
        Red,
        Yellow
    }

    public enum BoardPhase
    {
        Playing,
        Won,
        Deadlocked
    }

    public sealed class BoardActionResult
    {
        private BoardActionResult(
            bool succeeded,
            string message,
            int exitedMarbles,
            BoardPhase phase)
        {
            Succeeded = succeeded;
            Message = message;
            ExitedMarbles = exitedMarbles;
            Phase = phase;
        }

        public bool Succeeded { get; }
        public string Message { get; }
        public int ExitedMarbles { get; }
        public BoardPhase Phase { get; }

        public static BoardActionResult Success(
            string message,
            int exitedMarbles,
            BoardPhase phase)
        {
            return new BoardActionResult(
                true,
                message,
                exitedMarbles,
                phase);
        }

        public static BoardActionResult Failure(
            string message,
            BoardPhase phase)
        {
            return new BoardActionResult(false, message, 0, phase);
        }
    }

    public sealed class RingState
    {
        private readonly Dictionary<int, MarbleColor> _marbles;

        internal RingState(RingData data)
        {
            Id = data.id;
            Capacity = data.capacity;
            RotationOffset = BoardModel.Mod(
                data.rotationOffset,
                data.capacity);
            _marbles = new Dictionary<int, MarbleColor>();

            foreach (MarbleData marble in
                     data.marbles ?? Array.Empty<MarbleData>())
            {
                _marbles.Add(
                    marble.index,
                    MarbleColorUtility.Parse(marble.color));
            }
        }

        public string Id { get; }
        public int Capacity { get; }
        public int RotationOffset { get; internal set; }
        public IReadOnlyDictionary<int, MarbleColor> Marbles => _marbles;
        public int MarbleCount => _marbles.Count;
        public int GapCount => Capacity - MarbleCount;
        public bool CanRotate => GapCount > 0;

        internal bool TryGetMarbleAtWorldIndex(
            int worldIndex,
            int rotationOffset,
            out int localIndex,
            out MarbleColor color)
        {
            localIndex = BoardModel.Mod(
                worldIndex - rotationOffset,
                Capacity);
            return _marbles.TryGetValue(localIndex, out color);
        }

        internal bool IsWorldIndexEmpty(
            int worldIndex,
            int rotationOffset,
            out int localIndex)
        {
            localIndex = BoardModel.Mod(
                worldIndex - rotationOffset,
                Capacity);
            return !_marbles.ContainsKey(localIndex);
        }

        internal void RemoveMarble(int localIndex)
        {
            _marbles.Remove(localIndex);
        }

        internal void AddMarble(int localIndex, MarbleColor color)
        {
            _marbles.Add(localIndex, color);
        }

        internal Dictionary<int, MarbleColor> CopyMarbles()
        {
            return new Dictionary<int, MarbleColor>(_marbles);
        }

        internal void Restore(
            int rotationOffset,
            IReadOnlyDictionary<int, MarbleColor> marbles)
        {
            RotationOffset = rotationOffset;
            _marbles.Clear();
            foreach (KeyValuePair<int, MarbleColor> marble in marbles)
            {
                _marbles.Add(marble.Key, marble.Value);
            }
        }
    }

    public sealed class GateState
    {
        internal GateState(GateData data)
        {
            Id = data.id;
            FromRing = data.fromRing;
            ToRing = data.toRing;
            FromIndex = data.fromIndex;
            ToIndex = data.toIndex;
        }

        public string Id { get; }
        public string FromRing { get; }
        public string ToRing { get; }
        public int FromIndex { get; }
        public int ToIndex { get; }
    }

    public sealed class ExitState
    {
        internal ExitState(ExitData data)
        {
            Id = data.id;
            Ring = data.ring;
            RingIndex = data.ringIndex;
            Color = MarbleColorUtility.Parse(data.color);
        }

        public string Id { get; }
        public string Ring { get; }
        public int RingIndex { get; }
        public MarbleColor Color { get; }
    }

    public sealed class BoardModel
    {
        private readonly List<RingState> _rings;
        private readonly Dictionary<string, RingState> _ringById;
        private readonly Dictionary<string, int> _ringIndexById;
        private readonly int[] _offsetScratch;
        private readonly List<GateState> _gates;
        private readonly Dictionary<string, GateState> _gateById;
        private readonly List<ExitState> _exits;
        private readonly Stack<BoardSnapshot> _undo;
        private readonly BoardSnapshot _initialState;

        public BoardModel(LevelData level)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            LevelId = level.id;
            DisplayName = level.displayName;
            _rings = (level.rings ?? Array.Empty<RingData>())
                .Select(data => new RingState(data))
                .ToList();
            _ringById = _rings.ToDictionary(
                ring => ring.Id,
                StringComparer.OrdinalIgnoreCase);
            _ringIndexById = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < _rings.Count; index++)
            {
                _ringIndexById.Add(_rings[index].Id, index);
            }

            _offsetScratch = new int[_rings.Count];
            _gates = (level.gates ?? Array.Empty<GateData>())
                .Select(data => new GateState(data))
                .ToList();
            _gateById = _gates.ToDictionary(
                gate => gate.Id,
                StringComparer.OrdinalIgnoreCase);
            _exits = (level.exits ?? Array.Empty<ExitData>())
                .Select(data => new ExitState(data))
                .ToList();
            _undo = new Stack<BoardSnapshot>();

            ResolveAutomaticExits();
            Phase = EvaluatePhase();
            _initialState = Capture();
        }

        public string LevelId { get; }
        public string DisplayName { get; }
        public BoardPhase Phase { get; private set; }
        public IReadOnlyList<RingState> Rings => _rings;
        public IReadOnlyList<GateState> Gates => _gates;
        public IReadOnlyList<ExitState> Exits => _exits;
        public bool CanUndo => _undo.Count > 0;
        public int RemainingMarbles =>
            _rings.Sum(ring => ring.MarbleCount);

        public RingState GetRing(string ringId)
        {
            if (!_ringById.TryGetValue(ringId, out RingState ring))
            {
                throw new ArgumentException(
                    $"Unknown ring '{ringId}'.",
                    nameof(ringId));
            }

            return ring;
        }

        public GateState GetGate(string gateId)
        {
            if (!_gateById.TryGetValue(gateId, out GateState gate))
            {
                throw new ArgumentException(
                    $"Unknown gate '{gateId}'.",
                    nameof(gateId));
            }

            return gate;
        }

        public BoardActionResult TryRotateRing(
            string ringId,
            int deltaSteps)
        {
            if (Phase != BoardPhase.Playing)
            {
                return BoardActionResult.Failure(
                    "The level is not accepting input.",
                    Phase);
            }

            if (!_ringById.TryGetValue(ringId, out RingState ring))
            {
                return BoardActionResult.Failure(
                    $"Unknown ring '{ringId}'.",
                    Phase);
            }

            if (!ring.CanRotate)
            {
                return BoardActionResult.Failure(
                    $"{DisplayRingName(ring.Id)} is full and jammed.",
                    Phase);
            }

            int normalizedDelta = Mod(deltaSteps, ring.Capacity);
            if (normalizedDelta == 0)
            {
                return BoardActionResult.Failure(
                    "Rotate far enough to reach another position.",
                    Phase);
            }

            _undo.Push(Capture());
            ring.RotationOffset = Mod(
                ring.RotationOffset + deltaSteps,
                ring.Capacity);

            int exited = ResolveAutomaticExits();
            Phase = EvaluatePhase();
            return BoardActionResult.Success(
                exited > 0
                    ? $"Sorted {exited} marble{(exited == 1 ? "" : "s")}."
                    : $"{DisplayRingName(ring.Id)} rotated.",
                exited,
                Phase);
        }

        public BoardActionResult TryTransferGate(string gateId)
        {
            if (Phase != BoardPhase.Playing)
            {
                return BoardActionResult.Failure(
                    "The level is not accepting input.",
                    Phase);
            }

            if (!_gateById.TryGetValue(gateId, out GateState gate))
            {
                return BoardActionResult.Failure(
                    $"Unknown gate '{gateId}'.",
                    Phase);
            }

            RingState source = _ringById[gate.FromRing];
            RingState destination = _ringById[gate.ToRing];

            if (!source.TryGetMarbleAtWorldIndex(
                    gate.FromIndex,
                    source.RotationOffset,
                    out int sourceLocalIndex,
                    out MarbleColor color))
            {
                return BoardActionResult.Failure(
                    "No marble is aligned with this gate.",
                    Phase);
            }

            if (!destination.IsWorldIndexEmpty(
                    gate.ToIndex,
                    destination.RotationOffset,
                    out int destinationLocalIndex))
            {
                return BoardActionResult.Failure(
                    $"{DisplayRingName(destination.Id)} has no space "
                    + "at this gate.",
                    Phase);
            }

            _undo.Push(Capture());
            source.RemoveMarble(sourceLocalIndex);
            destination.AddMarble(destinationLocalIndex, color);

            int exited = ResolveAutomaticExits();
            Phase = EvaluatePhase();
            return BoardActionResult.Success(
                exited > 0
                    ? $"Transferred and sorted "
                      + $"{MarbleColorUtility.DisplayName(color)}."
                    : $"Transferred {MarbleColorUtility.DisplayName(color)} "
                      + "outward.",
                exited,
                Phase);
        }

        public BoardActionResult Undo()
        {
            if (_undo.Count == 0)
            {
                return BoardActionResult.Failure(
                    "Nothing to undo.",
                    Phase);
            }

            Restore(_undo.Pop());
            Phase = EvaluatePhase();
            return BoardActionResult.Success(
                "Previous state restored.",
                0,
                Phase);
        }

        public BoardActionResult Restart()
        {
            Restore(_initialState);
            _undo.Clear();
            Phase = EvaluatePhase();
            return BoardActionResult.Success(
                "Level restarted.",
                0,
                Phase);
        }

        public bool IsGateImmediatelyAvailable(string gateId)
        {
            if (!_gateById.TryGetValue(gateId, out GateState gate))
            {
                return false;
            }

            RingState source = _ringById[gate.FromRing];
            RingState destination = _ringById[gate.ToRing];
            return source.TryGetMarbleAtWorldIndex(
                       gate.FromIndex,
                       source.RotationOffset,
                       out _,
                       out _)
                   && destination.IsWorldIndexEmpty(
                       gate.ToIndex,
                       destination.RotationOffset,
                       out _);
        }

        private int ResolveAutomaticExits()
        {
            int removed = 0;
            foreach (ExitState exit in _exits)
            {
                RingState ring = _ringById[exit.Ring];
                if (ring.TryGetMarbleAtWorldIndex(
                        exit.RingIndex,
                        ring.RotationOffset,
                        out int localIndex,
                        out MarbleColor color)
                    && color == exit.Color)
                {
                    ring.RemoveMarble(localIndex);
                    removed++;
                }
            }

            return removed;
        }

        private BoardPhase EvaluatePhase()
        {
            if (RemainingMarbles == 0)
            {
                return BoardPhase.Won;
            }

            return HasReachableProductiveAction()
                ? BoardPhase.Playing
                : BoardPhase.Deadlocked;
        }

        private bool HasReachableProductiveAction()
        {
            return ExploreOffsets(0);
        }

        private bool ExploreOffsets(int ringIndex)
        {
            if (ringIndex >= _rings.Count)
            {
                return HasImmediateProductiveAction();
            }

            RingState ring = _rings[ringIndex];
            if (!ring.CanRotate || ring.MarbleCount == 0)
            {
                _offsetScratch[ringIndex] = ring.RotationOffset;
                return ExploreOffsets(ringIndex + 1);
            }

            for (int offset = 0; offset < ring.Capacity; offset++)
            {
                _offsetScratch[ringIndex] = offset;
                if (ExploreOffsets(ringIndex + 1))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasImmediateProductiveAction()
        {
            foreach (ExitState exit in _exits)
            {
                RingState ring = _ringById[exit.Ring];
                if (ring.TryGetMarbleAtWorldIndex(
                        exit.RingIndex,
                        _offsetScratch[_ringIndexById[ring.Id]],
                        out _,
                        out MarbleColor color)
                    && color == exit.Color)
                {
                    return true;
                }
            }

            foreach (GateState gate in _gates)
            {
                RingState source = _ringById[gate.FromRing];
                RingState destination = _ringById[gate.ToRing];
                if (source.TryGetMarbleAtWorldIndex(
                        gate.FromIndex,
                        _offsetScratch[_ringIndexById[source.Id]],
                        out _,
                        out _)
                    && destination.IsWorldIndexEmpty(
                        gate.ToIndex,
                        _offsetScratch[_ringIndexById[destination.Id]],
                        out _))
                {
                    return true;
                }
            }

            return false;
        }

        private BoardSnapshot Capture()
        {
            return new BoardSnapshot(
                _rings.Select(
                        ring => new RingSnapshot(
                            ring.Id,
                            ring.RotationOffset,
                            ring.CopyMarbles()))
                    .ToArray());
        }

        private void Restore(BoardSnapshot snapshot)
        {
            foreach (RingSnapshot ringSnapshot in snapshot.Rings)
            {
                _ringById[ringSnapshot.Id].Restore(
                    ringSnapshot.RotationOffset,
                    ringSnapshot.Marbles);
            }
        }

        public static int Mod(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static string DisplayRingName(string ringId)
        {
            if (string.IsNullOrWhiteSpace(ringId))
            {
                return "Ring";
            }

            return char.ToUpperInvariant(ringId[0]) + ringId.Substring(1)
                + " ring";
        }

        private sealed class BoardSnapshot
        {
            public BoardSnapshot(RingSnapshot[] rings)
            {
                Rings = rings;
            }

            public RingSnapshot[] Rings { get; }
        }

        private sealed class RingSnapshot
        {
            public RingSnapshot(
                string id,
                int rotationOffset,
                Dictionary<int, MarbleColor> marbles)
            {
                Id = id;
                RotationOffset = rotationOffset;
                Marbles = marbles;
            }

            public string Id { get; }
            public int RotationOffset { get; }
            public Dictionary<int, MarbleColor> Marbles { get; }
        }
    }

    public static class MarbleColorUtility
    {
        public static MarbleColor Parse(string value)
        {
            if (Enum.TryParse(
                    value,
                    true,
                    out MarbleColor color)
                && Enum.IsDefined(typeof(MarbleColor), color))
            {
                return color;
            }

            throw new ArgumentException(
                $"Unsupported marble color '{value}'.",
                nameof(value));
        }

        public static string DisplayName(MarbleColor color)
        {
            return color.ToString().ToLowerInvariant();
        }
    }
}
