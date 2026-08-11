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
