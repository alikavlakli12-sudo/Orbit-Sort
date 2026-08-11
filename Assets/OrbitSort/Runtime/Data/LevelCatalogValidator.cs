using System;
using System.Collections.Generic;
using System.Linq;

namespace OrbitSort.Data
{
    public static class LevelCatalogValidator
    {
        private static readonly string[] TwoRingIds =
        {
            "inner",
            "outer"
        };

        private static readonly string[] ThreeRingIds =
        {
            "inner",
            "middle",
            "outer"
        };

        private static readonly HashSet<string> SupportedColors =
            new HashSet<string>(
                new[] { "blue", "red", "yellow" },
                StringComparer.OrdinalIgnoreCase);

        public static void ValidateAndThrow(LevelCatalogData catalog)
        {
            IReadOnlyList<string> errors = Validate(catalog);
            if (errors.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "Invalid Orbit Sort level catalog:\n- "
                + string.Join("\n- ", errors));
        }

        public static IReadOnlyList<string> Validate(LevelCatalogData catalog)
        {
            List<string> errors = new List<string>();
            if (catalog == null)
            {
                errors.Add("Catalog is null.");
                return errors;
            }

            if (catalog.schemaVersion != 1)
            {
                errors.Add("schemaVersion must be 1.");
            }

            if (catalog.rules == null)
            {
                errors.Add("Prototype rules are required.");
            }
            else
            {
                if (catalog.rules.timerEnabled)
                {
                    errors.Add("The prototype cannot enable a timer.");
                }

                if (catalog.rules.moveCounterEnabled)
                {
                    errors.Add("The prototype cannot enable a move counter.");
                }

                if (!catalog.rules.ringRequiresGapToRotate)
                {
                    errors.Add("Rings must require a gap to rotate.");
                }

                if (!catalog.rules.gatesAreOneWay)
                {
                    errors.Add("Prototype gates must be one-way.");
                }

                if (!catalog.rules.deadlockWhenNoProgress)
                {
                    errors.Add("Deadlock loss must be enabled.");
                }
            }

            LevelData[] levels = catalog.levels ?? Array.Empty<LevelData>();
            if (levels.Length == 0)
            {
                errors.Add("At least one level is required.");
                return errors;
            }

            HashSet<string> levelIds =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < levels.Length; index++)
            {
                LevelData level = levels[index];
                string context = $"levels[{index}]";
                if (level == null)
                {
                    errors.Add($"{context} is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(level.id))
                {
                    errors.Add($"{context} needs an ID.");
                }
                else if (!levelIds.Add(level.id))
                {
                    errors.Add($"{context} duplicates level ID '{level.id}'.");
                }

                ValidateLevel(level, context, errors);
            }

            return errors;
        }

        private static void ValidateLevel(
            LevelData level,
            string context,
            List<string> errors)
        {
            RingData[] rings = level.rings ?? Array.Empty<RingData>();
            if (rings.Length < 2 || rings.Length > 3)
            {
                errors.Add($"{context} must contain two or three rings.");
                return;
            }

            string[] expectedRingIds = rings.Length == 2
                ? TwoRingIds
                : ThreeRingIds;

            Dictionary<string, RingData> ringById =
                new Dictionary<string, RingData>(
                    StringComparer.OrdinalIgnoreCase);
            HashSet<string> marbleColors =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int ringIndex = 0; ringIndex < rings.Length; ringIndex++)
            {
                RingData ring = rings[ringIndex];
                string ringContext = $"{context}.rings[{ringIndex}]";
                if (ring == null || string.IsNullOrWhiteSpace(ring.id))
                {
                    errors.Add($"{ringContext} needs an ID.");
                    continue;
                }

                if (!ringById.TryAdd(ring.id, ring))
                {
                    errors.Add($"{ringContext} duplicates ring ID '{ring.id}'.");
                    continue;
                }

                if (!string.Equals(
                        ring.id,
                        expectedRingIds[ringIndex],
                        StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"{ringContext}.id must be "
                        + $"'{expectedRingIds[ringIndex]}'.");
                }

                if (ring.capacity < 4 || ring.capacity > 64)
                {
                    errors.Add(
                        $"{ringContext}.capacity must be between 4 and 64.");
                    continue;
                }

                if (ring.rotationOffset < 0
                    || ring.rotationOffset >= ring.capacity)
                {
                    errors.Add(
                        $"{ringContext}.rotationOffset is outside capacity.");
                }

                MarbleData[] marbles =
                    ring.marbles ?? Array.Empty<MarbleData>();
                if (marbles.Length >= ring.capacity)
                {
                    errors.Add(
                        $"{ringContext} must start with at least one gap.");
                }

                HashSet<int> indexes = new HashSet<int>();
                for (int marbleIndex = 0;
                     marbleIndex < marbles.Length;
                     marbleIndex++)
                {
                    MarbleData marble = marbles[marbleIndex];
                    string marbleContext =
                        $"{ringContext}.marbles[{marbleIndex}]";
                    if (marble == null)
                    {
                        errors.Add($"{marbleContext} is null.");
                        continue;
                    }

                    if (marble.index < 0 || marble.index >= ring.capacity)
                    {
                        errors.Add(
                            $"{marbleContext}.index is outside capacity.");
                    }
                    else if (!indexes.Add(marble.index))
                    {
                        errors.Add(
                            $"{marbleContext}.index duplicates "
                            + $"{marble.index}.");
                    }

                    if (!SupportedColors.Contains(marble.color ?? string.Empty))
                    {
                        errors.Add(
                            $"{marbleContext} uses unsupported color "
                            + $"'{marble.color}'.");
                    }
                    else
                    {
                        marbleColors.Add(marble.color);
                    }
                }
            }

            ValidateGates(level, rings, ringById, context, errors);
            HashSet<string> exitColors =
                ValidateExits(level, rings, ringById, context, errors);

            foreach (string color in marbleColors.Except(
                         exitColors,
                         StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"{context} is missing an exit for '{color}'.");
            }
        }

        private static void ValidateGates(
            LevelData level,
            RingData[] rings,
            Dictionary<string, RingData> ringById,
            string context,
            List<string> errors)
        {
            GateData[] gates = level.gates ?? Array.Empty<GateData>();
            if (gates.Length != rings.Length - 1)
            {
                errors.Add(
                    $"{context} needs exactly {rings.Length - 1} "
                    + "outward gate"
                    + (rings.Length == 2 ? "." : "s."));
                return;
            }

            HashSet<string> ids =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool[] connectedPairs = new bool[rings.Length - 1];
            for (int index = 0; index < gates.Length; index++)
            {
                GateData gate = gates[index];
                string gateContext = $"{context}.gates[{index}]";
                if (gate == null || string.IsNullOrWhiteSpace(gate.id))
                {
                    errors.Add($"{gateContext} needs an ID.");
                    continue;
                }

                if (!ids.Add(gate.id))
                {
                    errors.Add($"{gateContext} duplicates ID '{gate.id}'.");
                }

                if (!ringById.TryGetValue(gate.fromRing, out RingData from)
                    || !ringById.TryGetValue(gate.toRing, out RingData to))
                {
                    errors.Add($"{gateContext} references an unknown ring.");
                    continue;
                }

                int fromOrder = Array.IndexOf(rings, from);
                int toOrder = Array.IndexOf(rings, to);
                if (toOrder != fromOrder + 1)
                {
                    errors.Add(
                        $"{gateContext} must connect adjacent rings outward.");
                }
                else if (connectedPairs[fromOrder])
                {
                    errors.Add(
                        $"{gateContext} duplicates an adjacent ring portal.");
                }
                else
                {
                    connectedPairs[fromOrder] = true;
                }

                if (gate.fromIndex < 0 || gate.fromIndex >= from.capacity
                    || gate.toIndex < 0 || gate.toIndex >= to.capacity)
                {
                    errors.Add($"{gateContext} contains an invalid index.");
                }

                if (!string.Equals(
                        gate.direction,
                        "outward",
                        StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"{gateContext} must point outward.");
                }
            }

            for (int index = 0; index < connectedPairs.Length; index++)
            {
                if (!connectedPairs[index])
                {
                    errors.Add(
                        $"{context} is missing the portal from "
                        + $"rings[{index}] to rings[{index + 1}].");
                }
            }
        }

        private static HashSet<string> ValidateExits(
            LevelData level,
            RingData[] rings,
            Dictionary<string, RingData> ringById,
            string context,
            List<string> errors)
        {
            HashSet<string> colors =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ExitData[] exits = level.exits ?? Array.Empty<ExitData>();
            string outerRingId = rings[rings.Length - 1].id;
            HashSet<int> indexes = new HashSet<int>();

            for (int index = 0; index < exits.Length; index++)
            {
                ExitData exit = exits[index];
                string exitContext = $"{context}.exits[{index}]";
                if (exit == null)
                {
                    errors.Add($"{exitContext} is null.");
                    continue;
                }

                if (!string.Equals(
                        exit.ring,
                        outerRingId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"{exitContext} must use outer ring '{outerRingId}'.");
                }

                if (!ringById.TryGetValue(exit.ring, out RingData ring)
                    || exit.ringIndex < 0
                    || exit.ringIndex >= ring.capacity)
                {
                    errors.Add($"{exitContext} contains an invalid ring index.");
                }
                else if (!indexes.Add(exit.ringIndex))
                {
                    errors.Add(
                        $"{exitContext} duplicates exit index "
                        + $"{exit.ringIndex}.");
                }

                if (!SupportedColors.Contains(exit.color ?? string.Empty))
                {
                    errors.Add(
                        $"{exitContext} uses unsupported color '{exit.color}'.");
                }
                else if (!colors.Add(exit.color))
                {
                    errors.Add(
                        $"{exitContext} duplicates color '{exit.color}'.");
                }
            }

            return colors;
        }
    }
}
