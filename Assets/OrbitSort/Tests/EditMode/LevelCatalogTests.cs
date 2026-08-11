using System;
using System.Linq;
using NUnit.Framework;
using OrbitSort.Core;
using OrbitSort.Data;

namespace OrbitSort.Tests.EditMode
{
    public sealed class LevelCatalogTests
    {
        private const string ValidCatalog = @"{
  ""schemaVersion"": 1,
  ""catalogId"": ""test"",
  ""rules"": {
    ""timerEnabled"": false,
    ""moveCounterEnabled"": false,
    ""ringRequiresGapToRotate"": true,
    ""gatesAreOneWay"": true,
    ""deadlockWhenNoProgress"": true
  },
  ""levels"": [{
    ""id"": ""level_test"",
    ""displayName"": ""Test"",
    ""rings"": [
      {""id"": ""inner"", ""capacity"": 4, ""rotationOffset"": 0,
       ""marbles"": [{""index"": 1, ""color"": ""blue""}]},
      {""id"": ""middle"", ""capacity"": 4, ""rotationOffset"": 0,
       ""marbles"": [{""index"": 2, ""color"": ""yellow""}]},
      {""id"": ""outer"", ""capacity"": 4, ""rotationOffset"": 0,
       ""marbles"": [{""index"": 2, ""color"": ""red""}]}
    ],
    ""gates"": [
      {
        ""id"": ""gate_inner_middle"", ""fromRing"": ""inner"",
        ""toRing"": ""middle"", ""fromIndex"": 0, ""toIndex"": 0,
        ""direction"": ""outward""
      },
      {
        ""id"": ""gate_middle_outer"", ""fromRing"": ""middle"",
        ""toRing"": ""outer"", ""fromIndex"": 2, ""toIndex"": 2,
        ""direction"": ""outward""
      }
    ],
    ""exits"": [
      {""id"": ""blue"", ""ring"": ""outer"", ""ringIndex"": 0,
       ""color"": ""blue""},
      {""id"": ""red"", ""ring"": ""outer"", ""ringIndex"": 1,
       ""color"": ""red""},
      {""id"": ""yellow"", ""ring"": ""outer"", ""ringIndex"": 3,
       ""color"": ""yellow""}
    ]
  }]
}";

        [Test]
        public void LoaderParsesAValidCatalog()
        {
            LevelCatalogData catalog =
                LevelCatalogLoader.Parse(ValidCatalog);

            Assert.That(catalog.schemaVersion, Is.EqualTo(1));
            Assert.That(catalog.levels, Has.Length.EqualTo(1));
        }

        [Test]
        public void PrototypeCatalogUsesTwoThenThreeRings()
        {
            LevelCatalogData catalog =
                LevelCatalogLoader.LoadFromResources();

            Assert.That(catalog.levels, Has.Length.EqualTo(5));
            Assert.That(catalog.levels[0].rings, Has.Length.EqualTo(2));
            Assert.That(catalog.levels[1].rings, Has.Length.EqualTo(2));
            Assert.That(catalog.levels[2].rings, Has.Length.EqualTo(3));
            Assert.That(catalog.levels[3].rings, Has.Length.EqualTo(3));
            Assert.That(catalog.levels[4].rings, Has.Length.EqualTo(3));
        }

        [Test]
        public void FirstLevelFillsEveryMeasuredSafeSlotWithTwoColors()
        {
            LevelData level =
                LevelCatalogLoader.LoadFromResources().levels[0];
            RingData inner = level.rings[0];
            RingData outer = level.rings[1];

            Assert.That(
                inner.capacity,
                Is.EqualTo(MaximumNonOverlappingSlots(1.80, 0.64)));
            Assert.That(
                outer.capacity,
                Is.EqualTo(MaximumNonOverlappingSlots(3.28, 0.64)));
            Assert.That(
                inner.marbles,
                Has.Length.EqualTo(inner.capacity - 1));
            Assert.That(
                outer.marbles,
                Has.Length.EqualTo(outer.capacity - 3));
            Assert.That(
                level.rings.Sum(ring => ring.marbles.Length),
                Is.EqualTo(45));
            Assert.That(
                level.rings
                    .SelectMany(ring => ring.marbles)
                    .Select(marble => marble.color)
                    .Distinct(StringComparer.OrdinalIgnoreCase),
                Is.EquivalentTo(new[] { "blue", "red" }));
            Assert.That(
                level.exits.Select(exit => exit.color),
                Is.EquivalentTo(new[] { "blue", "red" }));

            AssertEndpointSlotsStartEmpty(level);
        }

        [Test]
        public void SecondLevelFillsEveryMeasuredSafeSlotWithThreeColors()
        {
            LevelData level =
                LevelCatalogLoader.LoadFromResources().levels[1];
            RingData inner = level.rings[0];
            RingData outer = level.rings[1];

            Assert.That(
                inner.capacity,
                Is.EqualTo(MaximumNonOverlappingSlots(1.80, 0.64)));
            Assert.That(
                outer.capacity,
                Is.EqualTo(MaximumNonOverlappingSlots(3.28, 0.64)));
            Assert.That(
                inner.marbles,
                Has.Length.EqualTo(inner.capacity - 1));
            Assert.That(
                outer.marbles,
                Has.Length.EqualTo(outer.capacity - 4));
            Assert.That(
                level.rings.Sum(ring => ring.marbles.Length),
                Is.EqualTo(44));
            Assert.That(
                level.rings
                    .SelectMany(ring => ring.marbles)
                    .Select(marble => marble.color)
                    .Distinct(StringComparer.OrdinalIgnoreCase),
                Is.EquivalentTo(new[] { "blue", "red", "yellow" }));
            Assert.That(
                level.exits.Select(exit => exit.color),
                Is.EquivalentTo(new[] { "blue", "red", "yellow" }));

            AssertEndpointSlotsStartEmpty(level);
        }

        [Test]
        public void ThirdLevelFillsEveryMeasuredSafeSlotWithThreeColors()
        {
            LevelData level =
                LevelCatalogLoader.LoadFromResources().levels[2];
            RingData inner = level.rings[0];
            RingData middle = level.rings[1];
            RingData outer = level.rings[2];

            Assert.That(
                inner.capacity,
                Is.EqualTo(MaximumNonOverlappingSlots(1.80, 0.64)));
            Assert.That(
                middle.capacity,
                Is.EqualTo(MaximumNonOverlappingSlots(3.28, 0.64)));
            Assert.That(
                outer.capacity,
                Is.EqualTo(MaximumNonOverlappingSlots(4.76, 0.64)));
            Assert.That(
                inner.marbles,
                Has.Length.EqualTo(inner.capacity - 1));
            Assert.That(
                middle.marbles,
                Has.Length.EqualTo(middle.capacity - 2));
            Assert.That(
                outer.marbles,
                Has.Length.EqualTo(outer.capacity - 4));
            Assert.That(
                level.rings.Sum(ring => ring.marbles.Length),
                Is.EqualTo(88));
            Assert.That(
                level.rings
                    .SelectMany(ring => ring.marbles)
                    .Select(marble => marble.color)
                    .Distinct(StringComparer.OrdinalIgnoreCase),
                Is.EquivalentTo(new[] { "blue", "red", "yellow" }));
            Assert.That(
                level.exits.Select(exit => exit.color),
                Is.EquivalentTo(new[] { "blue", "red", "yellow" }));

            AssertEndpointSlotsStartEmpty(level);
        }

        [Test]
        public void FourthLevelFillsEveryMeasuredSafeSlotWithFourColors()
        {
            LevelData level =
                LevelCatalogLoader.LoadFromResources().levels[3];
            RingData inner = level.rings[0];
            RingData middle = level.rings[1];
            RingData outer = level.rings[2];

            Assert.That(
                inner.capacity,
                Is.EqualTo(MaximumNonOverlappingSlots(1.80, 0.64)));
            Assert.That(
                middle.capacity,
                Is.EqualTo(MaximumNonOverlappingSlots(3.28, 0.64)));
            Assert.That(
                outer.capacity,
                Is.EqualTo(MaximumNonOverlappingSlots(4.76, 0.64)));
            Assert.That(
                inner.marbles,
                Has.Length.EqualTo(inner.capacity - 1));
            Assert.That(
                middle.marbles,
                Has.Length.EqualTo(middle.capacity - 2));
            Assert.That(
                outer.marbles,
                Has.Length.EqualTo(outer.capacity - 5));
            Assert.That(
                level.rings.Sum(ring => ring.marbles.Length),
                Is.EqualTo(87));
            Assert.That(
                level.rings
                    .SelectMany(ring => ring.marbles)
                    .Select(marble => marble.color)
                    .Distinct(StringComparer.OrdinalIgnoreCase),
                Is.EquivalentTo(
                    new[] { "blue", "red", "yellow", "green" }));
            Assert.That(
                level.exits.Select(exit => exit.color),
                Is.EquivalentTo(
                    new[] { "blue", "red", "yellow", "green" }));

            AssertEndpointSlotsStartEmpty(level);
        }

        private static void AssertEndpointSlotsStartEmpty(LevelData level)
        {
            string[] endpointKeys = level.gates
                .SelectMany(gate => new[]
                {
                    $"{gate.fromRing}:{gate.fromIndex}",
                    $"{gate.toRing}:{gate.toIndex}"
                })
                .Concat(level.exits.Select(
                    exit => $"{exit.ring}:{exit.ringIndex}"))
                .ToArray();
            Assert.That(
                endpointKeys
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                Is.EqualTo(endpointKeys.Length),
                "Portal and receiver fronts must use distinct slots.");

            foreach (GateData gate in level.gates)
            {
                RingData source = level.rings.Single(
                    ring => ring.id == gate.fromRing);
                RingData destination = level.rings.Single(
                    ring => ring.id == gate.toRing);
                Assert.That(
                    source.marbles.Any(
                        marble => marble.index == gate.fromIndex),
                    Is.False,
                    $"{gate.id} source front must start empty.");
                Assert.That(
                    destination.marbles.Any(
                        marble => marble.index == gate.toIndex),
                    Is.False,
                    $"{gate.id} destination front must start empty.");
            }

            foreach (ExitData exit in level.exits)
            {
                RingData ring = level.rings.Single(
                    candidate => candidate.id == exit.ring);
                Assert.That(
                    ring.marbles.Any(
                        marble => marble.index == exit.ringIndex),
                    Is.False,
                    $"{exit.id} must start empty.");
            }
        }

        private static int MaximumNonOverlappingSlots(
            double ringRadius,
            double marbleDiameter)
        {
            return (int)Math.Floor(
                Math.PI
                / Math.Asin(marbleDiameter / (2d * ringRadius)));
        }

        [Test]
        public void EveryPrototypeLevelStartsPlayableWithAdjacentGates()
        {
            LevelCatalogData catalog =
                LevelCatalogLoader.LoadFromResources();

            foreach (LevelData level in catalog.levels)
            {
                var model = new BoardModel(level);
                int authoredMarbleCount = level.rings.Sum(
                    ring => ring.marbles.Length);
                Assert.That(
                    model.Phase,
                    Is.EqualTo(BoardPhase.Playing),
                    level.id);
                Assert.That(
                    model.RemainingMarbles,
                    Is.EqualTo(authoredMarbleCount),
                    $"{level.id} starts with a marble already sorted");
                Assert.That(
                    model.Gates,
                    Has.Count.EqualTo(model.Rings.Count - 1),
                    level.id);
            }
        }

        [Test]
        public void LoaderRejectsMoveCounter()
        {
            string invalid = ValidCatalog.Replace(
                @"""moveCounterEnabled"": false",
                @"""moveCounterEnabled"": true");

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(
                    () => LevelCatalogLoader.Parse(invalid));

            Assert.That(
                exception.Message,
                Does.Contain("move counter"));
        }
    }
}
