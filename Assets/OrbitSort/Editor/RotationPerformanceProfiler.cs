using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OrbitSort.Gameplay;
using OrbitSort.Presentation;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace OrbitSort.Editor
{
    [InitializeOnLoad]
    public static class RotationPerformanceProfiler
    {
        private const string RunSessionKey =
            "OrbitSort.RotationProfiler.Run";
        private const string ExitSessionKey =
            "OrbitSort.RotationProfiler.Exit";
        private const string LabelSessionKey =
            "OrbitSort.RotationProfiler.Label";
        private const int WarmupFrames = 90;
        private const int BaselineFrames = 180;
        private const int RotationFrames = 240;
        private const int SnapFrames = 90;

        private static readonly List<FrameSample> BaselineSamples =
            new List<FrameSample>();
        private static readonly List<FrameSample> RotationSamples =
            new List<FrameSample>();
        private static readonly List<FrameSample> SnapSamples =
            new List<FrameSample>();

        private static ProfilerRecorder _mainThreadRecorder;
        private static ProfilerRecorder _renderThreadRecorder;
        private static ProfilerRecorder _gcRecorder;
        private static ProfilerRecorder _drawCallsRecorder;
        private static ProfilerRecorder _setPassRecorder;
        private static ProfilerRecorder _trianglesRecorder;
        private static ProfilerRecorder _verticesRecorder;
        private static OrbitSortGameController _controller;
        private static OrbitSortBoardView _boardView;
        private static int _lastFrame = -1;
        private static int _profileFrame;
        private static bool _running;
        private static bool _dragStarted;
        private static bool _snapStarted;

        static RotationPerformanceProfiler()
        {
            EditorApplication.playModeStateChanged -=
                OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged +=
                OnPlayModeStateChanged;

            if (SessionState.GetBool(RunSessionKey, false)
                && EditorApplication.isPlaying)
            {
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
        }

        [MenuItem("Orbit Sort/Profile Live Ring Rotation")]
        public static void RunFromMenu()
        {
            BeginRun(false, "manual");
        }

        public static void RunFromCommandLine()
        {
            string label =
                Environment.GetEnvironmentVariable(
                    "ORBIT_SORT_PROFILE_LABEL")
                ?? "command-line";
            BeginRun(true, label);
        }

        private static void BeginRun(bool exitWhenDone, string label)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Stop the current Play Mode session before profiling.");
            }

            SessionState.SetBool(RunSessionKey, true);
            SessionState.SetBool(ExitSessionKey, exitWhenDone);
            SessionState.SetString(LabelSessionKey, label);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(
            PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode
                && SessionState.GetBool(RunSessionKey, false))
            {
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode
                && SessionState.GetBool(ExitSessionKey, false))
            {
                SessionState.EraseBool(ExitSessionKey);
                EditorApplication.delayCall += () =>
                    EditorApplication.Exit(0);
            }
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            try
            {
                if (!_running && !TryStartCapture())
                {
                    return;
                }

                if (Time.frameCount == _lastFrame)
                {
                    return;
                }

                _lastFrame = Time.frameCount;
                CaptureFrame();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                FinishCapture(exception.ToString());
            }
        }

        private static bool TryStartCapture()
        {
            _controller =
                UnityEngine.Object
                    .FindAnyObjectByType<OrbitSortGameController>();
            _boardView =
                UnityEngine.Object.FindAnyObjectByType<OrbitSortBoardView>();
            if (_controller == null
                || _controller.Model == null
                || _boardView == null)
            {
                return false;
            }

            BaselineSamples.Clear();
            RotationSamples.Clear();
            SnapSamples.Clear();
            _mainThreadRecorder = StartRecorder(
                ProfilerCategory.Internal,
                "Main Thread");
            _renderThreadRecorder = StartRecorder(
                ProfilerCategory.Internal,
                "Render Thread");
            _gcRecorder = StartRecorder(
                ProfilerCategory.Memory,
                "GC Allocated In Frame");
            _drawCallsRecorder = StartRecorder(
                ProfilerCategory.Render,
                "Draw Calls Count");
            _setPassRecorder = StartRecorder(
                ProfilerCategory.Render,
                "SetPass Calls Count");
            _trianglesRecorder = StartRecorder(
                ProfilerCategory.Render,
                "Triangles Count");
            _verticesRecorder = StartRecorder(
                ProfilerCategory.Render,
                "Vertices Count");

            _profileFrame = 0;
            _dragStarted = false;
            _snapStarted = false;
            _running = true;
            Debug.Log("Orbit Sort live rotation profile started.");
            return true;
        }

        private static ProfilerRecorder StartRecorder(
            ProfilerCategory category,
            string markerName)
        {
            return ProfilerRecorder.StartNew(category, markerName, 1);
        }

        private static void CaptureFrame()
        {
            _profileFrame++;
            if (_profileFrame <= WarmupFrames)
            {
                return;
            }

            int measuredFrame = _profileFrame - WarmupFrames;
            if (measuredFrame <= BaselineFrames)
            {
                BaselineSamples.Add(CreateSample());
                return;
            }

            int rotationFrame = measuredFrame - BaselineFrames;
            if (rotationFrame <= RotationFrames)
            {
                if (!_dragStarted)
                {
                    _dragStarted = _boardView.BeginRingDrag("outer");
                    if (!_dragStarted)
                    {
                        throw new InvalidOperationException(
                            "Could not begin the outer-ring profile drag.");
                    }
                }

                _boardView.PreviewRingRotation(
                    "outer",
                    rotationFrame * 2f);
                RotationSamples.Add(CreateSample());
                return;
            }

            int snapFrame = rotationFrame - RotationFrames;
            if (snapFrame <= SnapFrames)
            {
                if (!_snapStarted)
                {
                    _snapStarted = true;
                    _boardView.AnimateRingToModel(
                        "outer",
                        _controller.Model,
                        null);
                }

                SnapSamples.Add(CreateSample());
                return;
            }

            FinishCapture(null);
        }

        private static FrameSample CreateSample()
        {
            return new FrameSample
            {
                frameTimeMs = Time.unscaledDeltaTime * 1000f,
                mainThreadMs = RecorderMilliseconds(_mainThreadRecorder),
                renderThreadMs =
                    RecorderMilliseconds(_renderThreadRecorder),
                gcBytes = RecorderValue(_gcRecorder),
                drawCalls = RecorderValue(_drawCallsRecorder),
                setPassCalls = RecorderValue(_setPassRecorder),
                triangles = RecorderValue(_trianglesRecorder),
                vertices = RecorderValue(_verticesRecorder)
            };
        }

        private static float RecorderMilliseconds(
            ProfilerRecorder recorder)
        {
            long value = RecorderValue(recorder);
            return value < 0 ? -1f : value * 0.000001f;
        }

        private static long RecorderValue(ProfilerRecorder recorder)
        {
            return recorder.Valid ? recorder.LastValue : -1L;
        }

        private static void FinishCapture(string error)
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            EditorApplication.update -= Tick;
            DisposeRecorders();

            var report = new ProfileReport
            {
                label = SessionState.GetString(
                    LabelSessionKey,
                    "rotation"),
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                targetFrameRate = Application.targetFrameRate,
                error = error,
                baseline = BuildSummary("baseline", BaselineSamples),
                rotation = BuildSummary("rotation", RotationSamples),
                snap = BuildSummary("snap", SnapSamples),
                baselineFrames = BaselineSamples.ToArray(),
                rotationFrames = RotationSamples.ToArray(),
                snapFrames = SnapSamples.ToArray()
            };

            string json = JsonUtility.ToJson(report, true);
            string projectRoot =
                Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputDirectory =
                Path.Combine(projectRoot, "Library", "Profiling");
            Directory.CreateDirectory(outputDirectory);
            string filename =
                $"rotation-profile-{report.label}.json";
            string projectPath =
                Path.Combine(outputDirectory, filename);
            string temporaryPath =
                Path.Combine(Path.GetTempPath(), filename);
            File.WriteAllText(projectPath, json);
            File.WriteAllText(temporaryPath, json);
            Debug.Log(
                $"Orbit Sort live rotation profile: {projectPath}\n"
                + json);

            SessionState.SetBool(RunSessionKey, false);
            SessionState.EraseString(LabelSessionKey);
            EditorApplication.isPlaying = false;
        }

        private static void DisposeRecorders()
        {
            _mainThreadRecorder.Dispose();
            _renderThreadRecorder.Dispose();
            _gcRecorder.Dispose();
            _drawCallsRecorder.Dispose();
            _setPassRecorder.Dispose();
            _trianglesRecorder.Dispose();
            _verticesRecorder.Dispose();
        }

        private static PhaseSummary BuildSummary(
            string phase,
            IReadOnlyList<FrameSample> samples)
        {
            return new PhaseSummary
            {
                phase = phase,
                frames = samples.Count,
                frameTimeMeanMs = Mean(
                    samples.Select(sample => sample.frameTimeMs)),
                frameTimeP50Ms = Percentile(
                    samples.Select(sample => sample.frameTimeMs),
                    0.50f),
                frameTimeP95Ms = Percentile(
                    samples.Select(sample => sample.frameTimeMs),
                    0.95f),
                frameTimeMaxMs = Maximum(
                    samples.Select(sample => sample.frameTimeMs)),
                mainThreadMeanMs = Mean(
                    samples.Select(sample => sample.mainThreadMs)),
                mainThreadP95Ms = Percentile(
                    samples.Select(sample => sample.mainThreadMs),
                    0.95f),
                renderThreadMeanMs = Mean(
                    samples.Select(sample => sample.renderThreadMs)),
                renderThreadP95Ms = Percentile(
                    samples.Select(sample => sample.renderThreadMs),
                    0.95f),
                gcBytesMean = MeanLong(
                    samples.Select(sample => sample.gcBytes)),
                gcBytesMax = MaximumLong(
                    samples.Select(sample => sample.gcBytes)),
                drawCallsMean = MeanLong(
                    samples.Select(sample => sample.drawCalls)),
                setPassCallsMean = MeanLong(
                    samples.Select(sample => sample.setPassCalls)),
                trianglesMean = MeanLong(
                    samples.Select(sample => sample.triangles)),
                verticesMean = MeanLong(
                    samples.Select(sample => sample.vertices))
            };
        }

        private static float Mean(IEnumerable<float> values)
        {
            float[] valid = values
                .Where(value => value >= 0f)
                .ToArray();
            return valid.Length == 0 ? -1f : valid.Average();
        }

        private static long MeanLong(IEnumerable<long> values)
        {
            long[] valid = values
                .Where(value => value >= 0L)
                .ToArray();
            return valid.Length == 0
                ? -1L
                : (long)valid.Average(value => (double)value);
        }

        private static float Maximum(IEnumerable<float> values)
        {
            float[] valid = values
                .Where(value => value >= 0f)
                .ToArray();
            return valid.Length == 0 ? -1f : valid.Max();
        }

        private static long MaximumLong(IEnumerable<long> values)
        {
            long[] valid = values
                .Where(value => value >= 0L)
                .ToArray();
            return valid.Length == 0 ? -1L : valid.Max();
        }

        private static float Percentile(
            IEnumerable<float> values,
            float percentile)
        {
            float[] sorted = values
                .Where(value => value >= 0f)
                .OrderBy(value => value)
                .ToArray();
            if (sorted.Length == 0)
            {
                return -1f;
            }

            int index = Mathf.Clamp(
                Mathf.CeilToInt(sorted.Length * percentile) - 1,
                0,
                sorted.Length - 1);
            return sorted[index];
        }

        [Serializable]
        private sealed class ProfileReport
        {
            public string label;
            public string unityVersion;
            public string platform;
            public int targetFrameRate;
            public string error;
            public PhaseSummary baseline;
            public PhaseSummary rotation;
            public PhaseSummary snap;
            public FrameSample[] baselineFrames;
            public FrameSample[] rotationFrames;
            public FrameSample[] snapFrames;
        }

        [Serializable]
        private sealed class PhaseSummary
        {
            public string phase;
            public int frames;
            public float frameTimeMeanMs;
            public float frameTimeP50Ms;
            public float frameTimeP95Ms;
            public float frameTimeMaxMs;
            public float mainThreadMeanMs;
            public float mainThreadP95Ms;
            public float renderThreadMeanMs;
            public float renderThreadP95Ms;
            public long gcBytesMean;
            public long gcBytesMax;
            public long drawCallsMean;
            public long setPassCallsMean;
            public long trianglesMean;
            public long verticesMean;
        }

        [Serializable]
        private sealed class FrameSample
        {
            public float frameTimeMs;
            public float mainThreadMs;
            public float renderThreadMs;
            public long gcBytes;
            public long drawCalls;
            public long setPassCalls;
            public long triangles;
            public long vertices;
        }
    }
}
