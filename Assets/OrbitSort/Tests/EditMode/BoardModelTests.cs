using System;
using System.Linq;
using NUnit.Framework;
using OrbitSort.Core;
using OrbitSort.Data;

namespace OrbitSort.Tests.EditMode
{
    public sealed class BoardModelTests
    {
        [Test]
        public void FullRingCannotRotateWhileAnotherActionExists()
        {
            LevelData level = CreateTransferLevel(
                new[]
                {
                    Marble(0, "blue"),
                    Marble(1, "red"),
                    Marble(2, "yellow"),
                    Marble(3, "blue")
                },
                new[] { Marble(1, "red") });
            BoardModel model = new BoardModel(level);

            BoardActionResult result =
                model.TryRotateRing("inner", 1);

            Assert.That(model.Phase, Is.EqualTo(BoardPhase.Playing));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("full and jammed"));
        }

        [Test]
        public void AlignedGateTransfersOneMarbleOutward()
        {
            LevelData level = CreateTransferLevel(
                new[] { Marble(0, "blue") },
                new[] { Marble(1, "red") });
            BoardModel model = new BoardModel(level);

            BoardActionResult result =
                model.TryTransferGate("gate_inner_outer");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(model.GetRing("inner").MarbleCount, Is.Zero);
            Assert.That(model.GetRing("outer").MarbleCount, Is.EqualTo(2));
        }

        [Test]
        public void FillingFinalOuterGapCanCreateDeadlock()
        {
            BoardModel model = new BoardModel(CreateJamLesson());

            BoardActionResult result =
                model.TryTransferGate("gate_inner_outer");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(model.GetRing("outer").GapCount, Is.Zero);
            Assert.That(model.Phase, Is.EqualTo(BoardPhase.Deadlocked));
        }

        [Test]
        public void UndoRecoversFromDeadlock()
        {
            BoardModel model = new BoardModel(CreateJamLesson());
            model.TryTransferGate("gate_inner_outer");

            BoardActionResult result = model.Undo();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(model.Phase, Is.EqualTo(BoardPhase.Playing));
            Assert.That(model.GetRing("outer").GapCount, Is.EqualTo(1));
        }

        [Test]
        public void MatchingOuterMarbleExitsAndWins()
        {
            LevelData level = CreateTransferLevel(
                Array.Empty<MarbleData>(),
                new[] { Marble(0, "blue") });
            level.exits = new[]
            {
                Exit("exit_blue", 0, "blue")
            };

            BoardModel model = new BoardModel(level);

            Assert.That(model.RemainingMarbles, Is.Zero);
            Assert.That(model.Phase, Is.EqualTo(BoardPhase.Won));
        }

        [Test]
        public void RotationUsesHiddenPositionsWithoutVisibleSockets()
        {
            LevelData level = CreateTransferLevel(
                new[] { Marble(1, "blue") },
                new[] { Marble(2, "red") });
            BoardModel model = new BoardModel(level);

            BoardActionResult result =
                model.TryRotateRing("inner", -1);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(
                model.IsGateImmediatelyAvailable("gate_inner_outer"),
                Is.True);
        }

        private static LevelData CreateJamLesson()
        {
            return new LevelData
            {
                id = "jam_test",
                displayName = "Jam Test",
                rings = new[]
                {
                    Ring(
                        "inner",
                        Marble(0, "yellow"),
                        Marble(1, "blue")),
                    Ring(
                        "outer",
                        Marble(0, "red"),
                        Marble(1, "yellow"),
                        Marble(2, "blue"))
                },
                gates = new[]
                {
                    new GateData
                    {
                        id = "gate_inner_outer",
                        fromRing = "inner",
                        toRing = "outer",
                        fromIndex = 0,
                        toIndex = 3,
                        direction = "outward"
                    }
                },
                exits = new[]
                {
                    Exit("exit_blue", 0, "blue"),
                    Exit("exit_red", 1, "red"),
                    Exit("exit_yellow", 2, "yellow")
                }
            };
        }

        private static LevelData CreateTransferLevel(
            MarbleData[] inner,
            MarbleData[] outer)
        {
            return new LevelData
            {
                id = "test_level",
                displayName = "Test Level",
                rings = new[]
                {
                    Ring("inner", inner),
                    Ring("outer", outer)
                },
                gates = new[]
                {
                    new GateData
                    {
                        id = "gate_inner_outer",
                        fromRing = "inner",
                        toRing = "outer",
                        fromIndex = 0,
                        toIndex = 0,
                        direction = "outward"
                    }
                },
                exits = new[]
                {
                    Exit("exit_blue", 2, "blue"),
                    Exit("exit_red", 3, "red"),
                    Exit("exit_yellow", 1, "yellow")
                }
            };
        }

        private static RingData Ring(
            string id,
            params MarbleData[] marbles)
        {
            return new RingData
            {
                id = id,
                capacity = 4,
                rotationOffset = 0,
                marbles = marbles
            };
        }

        private static MarbleData Marble(int index, string color)
        {
            return new MarbleData
            {
                index = index,
                color = color
            };
        }

        private static ExitData Exit(
            string id,
            int ringIndex,
            string color)
        {
            return new ExitData
            {
                id = id,
                ring = "outer",
                ringIndex = ringIndex,
                color = color
            };
        }
    }
}
