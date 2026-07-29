using System;
using NUnit.Framework;
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
      {""id"": ""outer"", ""capacity"": 4, ""rotationOffset"": 0,
       ""marbles"": [{""index"": 2, ""color"": ""red""}]}
    ],
    ""gates"": [{
      ""id"": ""gate"", ""fromRing"": ""inner"", ""toRing"": ""outer"",
      ""fromIndex"": 0, ""toIndex"": 0, ""direction"": ""outward""
    }],
    ""exits"": [
      {""id"": ""blue"", ""ring"": ""outer"", ""ringIndex"": 0,
       ""color"": ""blue""},
      {""id"": ""red"", ""ring"": ""outer"", ""ringIndex"": 1,
       ""color"": ""red""}
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
