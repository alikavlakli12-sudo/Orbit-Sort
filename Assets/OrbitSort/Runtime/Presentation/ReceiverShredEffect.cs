using System;
using System.Collections;
using System.Collections.Generic;
using OrbitSort.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace OrbitSort.Presentation
{
    internal readonly struct ReceiverShredTarget
    {
        public ReceiverShredTarget(
            GameObject marble,
            Renderer[] renderers,
            MarbleColor color,
            Vector3 startWorldPosition,
            Vector3 receiverWorldPosition,
            Vector3 outwardWorldDirection,
            Vector3 fullScale)
        {
            Marble = marble;
            Renderers = renderers;
            Color = color;
            StartWorldPosition = startWorldPosition;
            ReceiverWorldPosition = receiverWorldPosition;
            OutwardWorldDirection = outwardWorldDirection;
            FullScale = fullScale;
        }

        public GameObject Marble { get; }
        public Renderer[] Renderers { get; }
        public MarbleColor Color { get; }
        public Vector3 StartWorldPosition { get; }
        public Vector3 ReceiverWorldPosition { get; }
        public Vector3 OutwardWorldDirection { get; }
        public Vector3 FullScale { get; }
    }

    [DisallowMultipleComponent]
    public sealed class ReceiverShredEffect : MonoBehaviour
    {
        private const int FragmentCount = 22;
        private const int RayCount = 12;
        private const int SparkCount = 10;
        private const float LatchDuration = 0.10f;
        private const float IntakeDuration = 0.92f;
        private const float DebrisTailDuration = 0.74f;
        private const float ShredStartProgress = 0.48f;
        private const float ReceiverMouthOffset = 0.38f;
        private const float ReceiverExitOffset = 0.26f;
        private const float ReceiverFinalOffset = 0.10f;

        private static readonly int ColorId =
            Shader.PropertyToID("_Color");
        private static readonly int ClipEnabledId =
            Shader.PropertyToID("_ClipEnabled");
        private static readonly int ClipPlaneId =
            Shader.PropertyToID("_ClipPlane");

        private readonly List<PooledFragment> _fragmentPool =
            new List<PooledFragment>();
        private readonly List<EffectRig> _rigPool =
            new List<EffectRig>();
        private readonly List<ActiveFragment> _activeFragments =
            new List<ActiveFragment>();
        private readonly List<TargetState> _targetStates =
            new List<TargetState>();
        private readonly List<UnityEngine.Object> _generatedAssets =
            new List<UnityEngine.Object>();
        private MaterialPropertyBlock _propertyBlock;

        private Transform _dynamicRoot;
        private IReadOnlyDictionary<MarbleColor, Material> _marbleMaterials;
        private Mesh[] _fragmentMeshes;
        private Mesh _haloMesh;
        private Mesh _glowMesh;
        private Mesh _streakMesh;
        private Material _glowMaterial;

        internal void Configure(
            Transform dynamicRoot,
            int maximumSimultaneousMarbles,
            IReadOnlyDictionary<MarbleColor, Material> marbleMaterials)
        {
            _dynamicRoot = dynamicRoot;
            _marbleMaterials = marbleMaterials;
            _propertyBlock ??= new MaterialPropertyBlock();
            _fragmentPool.Clear();
            _rigPool.Clear();
            _activeFragments.Clear();
            _targetStates.Clear();

            EnsureEffectAssets();
            CreateFragmentPool(
                maximumSimultaneousMarbles * FragmentCount);
            CreateRigPool(maximumSimultaneousMarbles);
        }

        internal IEnumerator Animate(
            IReadOnlyList<ReceiverShredTarget> targets,
            Action onComplete)
        {
            PrepareTargets(targets);
            float elapsed = 0f;
            float totalDuration = IntakeDuration + DebrisTailDuration;

            while (elapsed < totalDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float intakeProgress =
                    Mathf.Clamp01(elapsed / IntakeDuration);

                foreach (TargetState state in _targetStates)
                {
                    if (elapsed <= IntakeDuration)
                    {
                        UpdateMarbleIntake(state, intakeProgress);
                        EmitProgressiveFragments(
                            state,
                            intakeProgress,
                            elapsed);
                    }

                    UpdateLightRig(state, intakeProgress, elapsed);
                }

                UpdateFragments(elapsed);
                yield return null;
            }

            FinishAnimation();
            onComplete?.Invoke();
        }

        private void PrepareTargets(
            IReadOnlyList<ReceiverShredTarget> targets)
        {
            _activeFragments.Clear();
            _targetStates.Clear();

            foreach (ReceiverShredTarget target in targets)
            {
                EffectRig rig = GetAvailableRig();
                if (rig == null)
                {
                    continue;
                }

                Vector3 outward = target.OutwardWorldDirection.normalized;
                Vector3 mouth = target.ReceiverWorldPosition
                                - outward * ReceiverMouthOffset;
                mouth.z = target.StartWorldPosition.z;
                Vector3 final = target.ReceiverWorldPosition
                                + outward * ReceiverFinalOffset;
                final.z = target.StartWorldPosition.z;

                rig.Root.SetActive(true);
                var state = new TargetState(
                    target,
                    rig,
                    mouth,
                    final);
                ApplyMarbleClip(state);
                _targetStates.Add(state);
            }
        }

        private void UpdateMarbleIntake(
            TargetState state,
            float intakeProgress)
        {
            float movementProgress = SmootherStep(
                Mathf.InverseLerp(
                    LatchDuration / IntakeDuration,
                    1f,
                    intakeProgress));

            if (state.Target.Marble != null)
            {
                Transform marble = state.Target.Marble.transform;
                marble.position = Vector3.LerpUnclamped(
                    state.Target.StartWorldPosition,
                    state.FinalWorldPosition,
                    movementProgress);
                marble.localScale = state.Target.FullScale;
            }

            if (intakeProgress >= 1f && !state.MarbleReleased)
            {
                state.MarbleReleased = true;
                if (state.Target.Marble != null)
                {
                    state.Target.Marble.SetActive(false);
                    ReleaseObject(state.Target.Marble);
                }
            }
        }

        private void EmitProgressiveFragments(
            TargetState state,
            float intakeProgress,
            float elapsed)
        {
            float shredProgress = Mathf.InverseLerp(
                ShredStartProgress,
                1f,
                intakeProgress);
            int desiredCount = Mathf.Min(
                FragmentCount,
                Mathf.FloorToInt(shredProgress * FragmentCount + 0.001f));

            while (state.EmittedFragmentCount < desiredCount)
            {
                ActivateFragment(
                    state,
                    state.EmittedFragmentCount,
                    elapsed);
                state.EmittedFragmentCount++;
            }
        }

        private void UpdateLightRig(
            TargetState state,
            float intakeProgress,
            float elapsed)
        {
            EffectRig rig = state.Rig;
            Vector3 outward = state.Target.OutwardWorldDirection.normalized;
            Vector3 tangent = new Vector3(-outward.y, outward.x, 0f);
            Vector3 marblePosition = state.Target.Marble != null
                ? state.Target.Marble.transform.position
                : state.FinalWorldPosition;

            float latch = SmootherStep(
                Mathf.InverseLerp(0f, 0.10f, intakeProgress));
            float haloFade = 1f - SmootherStep(
                Mathf.InverseLerp(0.72f, 0.92f, intakeProgress));
            float haloIntensity = latch * haloFade;
            float pulse = 1f + Mathf.Sin(elapsed * 12f) * 0.045f;
            SetVisualTransform(
                rig.Halo,
                new Vector3(
                    marblePosition.x,
                    marblePosition.y,
                    marblePosition.z - 0.16f),
                0f,
                new Vector3(pulse, pulse, 1f));
            SetGlowColor(
                rig.Halo.Renderer,
                new Color(1f, 0.92f, 0.98f, haloIntensity));

            float suctionFade = intakeProgress < 1f
                ? SmootherStep(Mathf.InverseLerp(0.06f, 0.28f, intakeProgress))
                : 1f - SmootherStep(
                    Mathf.InverseLerp(
                        IntakeDuration,
                        IntakeDuration + 0.18f,
                        elapsed));
            Vector3 glowPosition = Vector3.Lerp(
                state.MouthWorldPosition,
                marblePosition,
                0.38f);
            float gap = Vector3.Distance(
                state.MouthWorldPosition,
                marblePosition);
            SetVisualTransform(
                rig.AmbientGlow,
                new Vector3(
                    glowPosition.x,
                    glowPosition.y,
                    marblePosition.z - 0.10f),
                0f,
                Vector3.one * Mathf.Lerp(0.48f, 0.82f, gap));
            SetGlowColor(
                rig.AmbientGlow.Renderer,
                new Color(0.78f, 0.12f, 0.92f, 0.24f * suctionFade));

            UpdateRays(
                rig,
                state.MouthWorldPosition,
                marblePosition,
                outward,
                tangent,
                suctionFade,
                elapsed);
            UpdateSparks(
                rig,
                state,
                marblePosition,
                outward,
                tangent,
                suctionFade,
                intakeProgress,
                elapsed);

            float flashIntensity = intakeProgress < 1f
                ? SmootherStep(
                    Mathf.InverseLerp(0.56f, 0.96f, intakeProgress))
                : 1f - SmootherStep(
                    Mathf.InverseLerp(
                        IntakeDuration,
                        IntakeDuration + 0.28f,
                        elapsed));
            float tangentAngle = Mathf.Atan2(tangent.y, tangent.x)
                                 * Mathf.Rad2Deg;
            SetVisualTransform(
                rig.MouthFlash,
                new Vector3(
                    state.MouthWorldPosition.x,
                    state.MouthWorldPosition.y,
                    state.MouthWorldPosition.z - 0.20f),
                tangentAngle,
                new Vector3(
                    0.72f + flashIntensity * 0.20f,
                    1.2f + flashIntensity * 1.8f,
                    1f));
            SetGlowColor(
                rig.MouthFlash.Renderer,
                new Color(1f, 0.38f, 0.90f, flashIntensity));
        }

        private void UpdateRays(
            EffectRig rig,
            Vector3 mouth,
            Vector3 marble,
            Vector3 outward,
            Vector3 tangent,
            float intensity,
            float elapsed)
        {
            float baseAngle = Mathf.Atan2(outward.y, outward.x)
                              * Mathf.Rad2Deg;
            float gap = Mathf.Max(0.18f, Vector3.Distance(mouth, marble));

            for (int index = 0; index < rig.Rays.Length; index++)
            {
                float cycle = Mathf.Repeat(
                    elapsed * (0.58f + index * 0.031f)
                    + index * 0.173f,
                    1f);
                float centerProgress = Mathf.Lerp(0.16f, 0.82f, cycle);
                float lateral = Mathf.Sin(index * 2.31f + elapsed * 4.2f)
                                * (0.06f + index % 3 * 0.022f);
                Vector3 position = Vector3.Lerp(mouth, marble, centerProgress)
                                   + tangent * lateral;
                position.z = mouth.z - 0.12f;
                float length = Mathf.Min(
                    gap * 0.54f,
                    0.18f + (index % 5) * 0.055f);
                float flicker = 0.42f
                                + 0.58f
                                * Mathf.Abs(
                                    Mathf.Sin(elapsed * 17f + index * 1.7f));

                SetVisualTransform(
                    rig.Rays[index],
                    position,
                    baseAngle + Mathf.Sin(index * 1.37f) * 8f,
                    new Vector3(length, 0.65f + index % 3 * 0.20f, 1f));
                SetGlowColor(
                    rig.Rays[index].Renderer,
                    new Color(1f, 0.24f, 0.82f, intensity * flicker * 0.82f));
            }
        }

        private void UpdateSparks(
            EffectRig rig,
            TargetState state,
            Vector3 marble,
            Vector3 outward,
            Vector3 tangent,
            float suctionIntensity,
            float intakeProgress,
            float elapsed)
        {
            for (int index = 0; index < rig.Sparks.Length; index++)
            {
                float speed = 0.72f + index * 0.047f;
                float cycle = Mathf.Repeat(
                    elapsed * speed + index * 0.193f,
                    1f);
                float contactBlend = SmootherStep(
                    Mathf.InverseLerp(0.48f, 0.86f, intakeProgress));
                Vector3 gapPosition = Vector3.Lerp(
                    state.MouthWorldPosition,
                    marble,
                    Mathf.Lerp(0.10f, 0.82f, cycle));
                Vector3 burstPosition = state.Target.ReceiverWorldPosition
                    + outward * (ReceiverExitOffset + cycle * 0.48f)
                    + tangent
                    * Mathf.Sin(index * 2.7f + cycle * 6.28f)
                    * 0.16f;
                Vector3 position = Vector3.Lerp(
                    gapPosition,
                    burstPosition,
                    contactBlend);
                position.z = state.MouthWorldPosition.z - 0.18f;
                float twinkle = Mathf.Abs(
                    Mathf.Sin(elapsed * 21f + index * 2.4f));
                float scale = Mathf.Lerp(0.025f, 0.065f, twinkle);

                SetVisualTransform(
                    rig.Sparks[index],
                    position,
                    0f,
                    Vector3.one * scale);
                SetGlowColor(
                    rig.Sparks[index].Renderer,
                    new Color(
                        1f,
                        0.46f,
                        0.92f,
                        suctionIntensity * (0.35f + twinkle * 0.65f)));
            }
        }

        private void ApplyMarbleClip(TargetState state)
        {
            Vector3 outward = state.Target.OutwardWorldDirection.normalized;
            Vector4 clipPlane = new Vector4(
                -outward.x,
                -outward.y,
                -outward.z,
                Vector3.Dot(outward, state.MouthWorldPosition));

            foreach (Renderer renderer in state.Target.Renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(ClipEnabledId, 1f);
                _propertyBlock.SetVector(ClipPlaneId, clipPlane);
                renderer.SetPropertyBlock(_propertyBlock);
                _propertyBlock.Clear();
            }
        }

        private void ActivateFragment(
            TargetState state,
            int index,
            float birthTime)
        {
            PooledFragment pooledFragment = GetAvailableFragment();
            if (pooledFragment == null)
            {
                return;
            }

            Vector3 outward = state.Target.OutwardWorldDirection.normalized;
            Vector3 tangent = new Vector3(-outward.y, outward.x, 0f);
            float centeredIndex = (index % 7) - 3f;
            float lateral = centeredIndex * 0.105f
                            + Mathf.Sin(index * 2.17f) * 0.055f;
            Vector3 start = state.Target.ReceiverWorldPosition
                            + outward * ReceiverExitOffset
                            + tangent * Mathf.Sin(index * 1.73f) * 0.045f;
            start.z = state.Target.ReceiverWorldPosition.z - 0.10f;
            Vector3 end = start
                + outward * (1.02f + index % 6 * 0.15f)
                + tangent * lateral
                + new Vector3(0f, 0f, -0.03f - index % 3 * 0.015f);

            float baseScale = index % 6 == 0
                ? 0.92f
                : 0.38f + (index % 5) * 0.105f;
            float startAngle = index * 47f;
            float spin = (index % 2 == 0 ? -1f : 1f)
                         * (270f + index % 5 * 72f);
            Quaternion startRotation = Quaternion.Euler(
                index * 13f,
                index * 19f,
                startAngle);
            Quaternion endRotation = Quaternion.Euler(
                70f + index * 17f,
                44f + index * 23f,
                startAngle + spin);
            float duration = 0.66f + index % 4 * 0.075f;

            GameObject fragmentObject = pooledFragment.GameObject;
            fragmentObject.name =
                $"{MarbleColorUtility.DisplayName(state.Target.Color)} "
                + "Shred Fragment";
            fragmentObject.SetActive(true);
            fragmentObject.transform.position = start;
            fragmentObject.transform.rotation = startRotation;
            fragmentObject.transform.localScale = Vector3.zero;
            pooledFragment.Renderer.sharedMaterial =
                _marbleMaterials[state.Target.Color];

            _activeFragments.Add(
                new ActiveFragment(
                    fragmentObject.transform,
                    start,
                    end,
                    startRotation,
                    endRotation,
                    birthTime,
                    duration,
                    baseScale));
        }

        private void UpdateFragments(float elapsed)
        {
            foreach (ActiveFragment fragment in _activeFragments)
            {
                if (fragment.Transform == null
                    || !fragment.Transform.gameObject.activeSelf)
                {
                    continue;
                }

                float progress = Mathf.Clamp01(
                    (elapsed - fragment.BirthTime) / fragment.Duration);
                float inverse = 1f - progress;
                float launchProgress = 1f - inverse * inverse * inverse;
                float grow = SmootherStep(
                    Mathf.Clamp01(progress / 0.12f));
                float shrink = 1f - SmootherStep(
                    Mathf.InverseLerp(0.82f, 1f, progress));

                fragment.Transform.position = Vector3.LerpUnclamped(
                    fragment.StartWorldPosition,
                    fragment.EndWorldPosition,
                    launchProgress);
                fragment.Transform.rotation = Quaternion.SlerpUnclamped(
                    fragment.StartRotation,
                    fragment.EndRotation,
                    launchProgress);
                fragment.Transform.localScale = Vector3.one
                                                * (fragment.BaseScale
                                                   * grow
                                                   * shrink);

                if (progress >= 1f)
                {
                    fragment.Transform.gameObject.SetActive(false);
                }
            }
        }

        private void FinishAnimation()
        {
            foreach (TargetState state in _targetStates)
            {
                if (!state.MarbleReleased && state.Target.Marble != null)
                {
                    state.Target.Marble.SetActive(false);
                    ReleaseObject(state.Target.Marble);
                }

                state.Rig.Root.SetActive(false);
            }

            foreach (ActiveFragment fragment in _activeFragments)
            {
                if (fragment.Transform != null)
                {
                    fragment.Transform.gameObject.SetActive(false);
                }
            }

            _activeFragments.Clear();
            _targetStates.Clear();
        }

        private void CreateFragmentPool(int count)
        {
            for (int index = 0; index < count; index++)
            {
                GameObject fragmentObject =
                    new GameObject("Pooled Shred Fragment");
                fragmentObject.transform.SetParent(_dynamicRoot, false);

                MeshFilter meshFilter =
                    fragmentObject.AddComponent<MeshFilter>();
                meshFilter.sharedMesh =
                    _fragmentMeshes[index % _fragmentMeshes.Length];
                MeshRenderer meshRenderer =
                    fragmentObject.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                fragmentObject.SetActive(false);
                _fragmentPool.Add(
                    new PooledFragment(fragmentObject, meshRenderer));
            }
        }

        private void CreateRigPool(int count)
        {
            for (int index = 0; index < count; index++)
            {
                GameObject root = new GameObject("Pooled Receiver Light Rig");
                root.transform.SetParent(_dynamicRoot, false);
                EffectVisual halo = CreateEffectVisual(
                    "Selection Halo",
                    root.transform,
                    _haloMesh);
                EffectVisual ambientGlow = CreateEffectVisual(
                    "Suction Glow",
                    root.transform,
                    _glowMesh);
                EffectVisual mouthFlash = CreateEffectVisual(
                    "Receiver Mouth Flash",
                    root.transform,
                    _streakMesh);

                var rays = new EffectVisual[RayCount];
                for (int rayIndex = 0; rayIndex < rays.Length; rayIndex++)
                {
                    rays[rayIndex] = CreateEffectVisual(
                        $"Suction Ray {rayIndex + 1}",
                        root.transform,
                        _streakMesh);
                }

                var sparks = new EffectVisual[SparkCount];
                for (int sparkIndex = 0;
                     sparkIndex < sparks.Length;
                     sparkIndex++)
                {
                    sparks[sparkIndex] = CreateEffectVisual(
                        $"Suction Spark {sparkIndex + 1}",
                        root.transform,
                        _glowMesh);
                }

                root.SetActive(false);
                _rigPool.Add(
                    new EffectRig(
                        root,
                        halo,
                        ambientGlow,
                        mouthFlash,
                        rays,
                        sparks));
            }
        }

        private EffectVisual CreateEffectVisual(
            string objectName,
            Transform parent,
            Mesh mesh)
        {
            GameObject effectObject = new GameObject(objectName);
            effectObject.transform.SetParent(parent, false);
            MeshFilter meshFilter = effectObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            MeshRenderer meshRenderer =
                effectObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _glowMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return new EffectVisual(effectObject.transform, meshRenderer);
        }

        private PooledFragment GetAvailableFragment()
        {
            foreach (PooledFragment fragment in _fragmentPool)
            {
                if (!fragment.GameObject.activeSelf)
                {
                    return fragment;
                }
            }

            return null;
        }

        private EffectRig GetAvailableRig()
        {
            foreach (EffectRig rig in _rigPool)
            {
                if (!rig.Root.activeSelf)
                {
                    return rig;
                }
            }

            return null;
        }

        private void SetGlowColor(Renderer renderer, Color color)
        {
            _propertyBlock.Clear();
            _propertyBlock.SetColor(ColorId, color);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private static void SetVisualTransform(
            EffectVisual visual,
            Vector3 worldPosition,
            float angleDegrees,
            Vector3 scale)
        {
            visual.Transform.position = worldPosition;
            visual.Transform.rotation =
                Quaternion.Euler(0f, 0f, angleDegrees);
            visual.Transform.localScale = scale;
        }

        private void EnsureEffectAssets()
        {
            if (_fragmentMeshes != null)
            {
                return;
            }

            Shader glowShader = Resources.Load<Shader>(
                "Shaders/AdditiveGlow");
            if (glowShader == null)
            {
                glowShader = Shader.Find("OrbitSort/AdditiveGlow");
            }

            if (glowShader == null)
            {
                throw new InvalidOperationException(
                    "Orbit Sort additive glow shader could not be loaded.");
            }

            _glowMaterial = new Material(glowShader)
            {
                name = "Receiver Additive Glow",
                enableInstancing = true
            };
            _generatedAssets.Add(_glowMaterial);

            _fragmentMeshes = new[]
            {
                CreateTriangularPrismMesh(),
                CreateIrregularBoxMesh(),
                CreateTetrahedronMesh()
            };
            _haloMesh = CreateHaloMesh();
            _glowMesh = CreateRadialGlowMesh();
            _streakMesh = CreateStreakMesh();
        }

        private Mesh CreateTriangularPrismMesh()
        {
            return CreateMesh(
                "Triangular Shred Chunk",
                new[]
                {
                    new Vector3(-0.16f, -0.10f, -0.07f),
                    new Vector3(0.16f, -0.06f, -0.07f),
                    new Vector3(-0.04f, 0.18f, -0.07f),
                    new Vector3(-0.16f, -0.10f, 0.07f),
                    new Vector3(0.16f, -0.06f, 0.07f),
                    new Vector3(-0.04f, 0.18f, 0.07f)
                },
                new[]
                {
                    0, 2, 1, 3, 4, 5,
                    0, 1, 4, 0, 4, 3,
                    1, 2, 5, 1, 5, 4,
                    2, 0, 3, 2, 3, 5
                });
        }

        private Mesh CreateIrregularBoxMesh()
        {
            return CreateMesh(
                "Irregular Shred Chunk",
                new[]
                {
                    new Vector3(-0.14f, -0.11f, -0.08f),
                    new Vector3(0.16f, -0.09f, -0.06f),
                    new Vector3(0.13f, 0.14f, -0.08f),
                    new Vector3(-0.11f, 0.16f, -0.05f),
                    new Vector3(-0.12f, -0.10f, 0.09f),
                    new Vector3(0.14f, -0.08f, 0.07f),
                    new Vector3(0.11f, 0.12f, 0.10f),
                    new Vector3(-0.10f, 0.14f, 0.08f)
                },
                new[]
                {
                    0, 3, 2, 0, 2, 1,
                    4, 5, 6, 4, 6, 7,
                    0, 1, 5, 0, 5, 4,
                    1, 2, 6, 1, 6, 5,
                    2, 3, 7, 2, 7, 6,
                    3, 0, 4, 3, 4, 7
                });
        }

        private Mesh CreateTetrahedronMesh()
        {
            return CreateMesh(
                "Tetrahedral Shred Chunk",
                new[]
                {
                    new Vector3(0f, 0.18f, -0.08f),
                    new Vector3(-0.16f, -0.12f, -0.07f),
                    new Vector3(0.17f, -0.10f, -0.06f),
                    new Vector3(0.01f, 0.01f, 0.16f)
                },
                new[]
                {
                    0, 2, 1,
                    0, 3, 2,
                    2, 3, 1,
                    1, 3, 0
                });
        }

        private Mesh CreateHaloMesh()
        {
            const int segments = 40;
            var vertices = new Vector3[segments * 3];
            var colors = new Color[vertices.Length];
            var triangles = new int[segments * 12];
            float[] radii = { 0.31f, 0.39f, 0.50f };
            float[] alphas = { 0f, 1f, 0f };

            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                Vector3 direction = new Vector3(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle),
                    0f);
                for (int ring = 0; ring < 3; ring++)
                {
                    int vertexIndex = index * 3 + ring;
                    vertices[vertexIndex] = direction * radii[ring];
                    colors[vertexIndex] =
                        new Color(1f, 1f, 1f, alphas[ring]);
                }

                int next = (index + 1) % segments;
                int triangleIndex = index * 12;
                for (int band = 0; band < 2; band++)
                {
                    int currentInner = index * 3 + band;
                    int currentOuter = currentInner + 1;
                    int nextInner = next * 3 + band;
                    int nextOuter = nextInner + 1;
                    int offset = triangleIndex + band * 6;
                    triangles[offset] = currentInner;
                    triangles[offset + 1] = nextOuter;
                    triangles[offset + 2] = currentOuter;
                    triangles[offset + 3] = currentInner;
                    triangles[offset + 4] = nextInner;
                    triangles[offset + 5] = nextOuter;
                }
            }

            return CreateMesh(
                "Marble Selection Halo",
                vertices,
                triangles,
                colors);
        }

        private Mesh CreateRadialGlowMesh()
        {
            const int segments = 32;
            var vertices = new Vector3[segments + 1];
            var colors = new Color[vertices.Length];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            colors[0] = Color.white;
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                vertices[index + 1] = new Vector3(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle),
                    0f);
                colors[index + 1] = new Color(1f, 1f, 1f, 0f);
                int next = (index + 1) % segments;
                triangles[index * 3] = 0;
                triangles[index * 3 + 1] = next + 1;
                triangles[index * 3 + 2] = index + 1;
            }

            return CreateMesh(
                "Receiver Radial Glow",
                vertices,
                triangles,
                colors);
        }

        private Mesh CreateStreakMesh()
        {
            return CreateMesh(
                "Receiver Light Streak",
                new[]
                {
                    new Vector3(-0.5f, 0f, 0f),
                    new Vector3(0f, -0.045f, 0f),
                    new Vector3(0f, 0.045f, 0f),
                    new Vector3(0.5f, 0f, 0f)
                },
                new[]
                {
                    0, 2, 1,
                    1, 2, 3
                },
                new[]
                {
                    new Color(1f, 1f, 1f, 0f),
                    Color.white,
                    Color.white,
                    new Color(1f, 1f, 1f, 0f)
                });
        }

        private Mesh CreateMesh(
            string meshName,
            Vector3[] vertices,
            int[] triangles,
            Color[] colors = null)
        {
            var mesh = new Mesh
            {
                name = meshName,
                vertices = vertices,
                triangles = triangles
            };
            if (colors != null)
            {
                mesh.colors = colors;
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            _generatedAssets.Add(mesh);
            return mesh;
        }

        private static float SmootherStep(float value)
        {
            float t = Mathf.Clamp01(value);
            return t
                   * t
                   * t
                   * (t * (t * 6f - 15f) + 10f);
        }

        private static void ReleaseObject(UnityEngine.Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void OnDestroy()
        {
            foreach (UnityEngine.Object asset in _generatedAssets)
            {
                if (asset != null)
                {
                    ReleaseObject(asset);
                }
            }

            _generatedAssets.Clear();
        }

        private sealed class TargetState
        {
            public TargetState(
                ReceiverShredTarget target,
                EffectRig rig,
                Vector3 mouthWorldPosition,
                Vector3 finalWorldPosition)
            {
                Target = target;
                Rig = rig;
                MouthWorldPosition = mouthWorldPosition;
                FinalWorldPosition = finalWorldPosition;
            }

            public ReceiverShredTarget Target { get; }
            public EffectRig Rig { get; }
            public Vector3 MouthWorldPosition { get; }
            public Vector3 FinalWorldPosition { get; }
            public int EmittedFragmentCount { get; set; }
            public bool MarbleReleased { get; set; }
        }

        private readonly struct ActiveFragment
        {
            public ActiveFragment(
                Transform transform,
                Vector3 startWorldPosition,
                Vector3 endWorldPosition,
                Quaternion startRotation,
                Quaternion endRotation,
                float birthTime,
                float duration,
                float baseScale)
            {
                Transform = transform;
                StartWorldPosition = startWorldPosition;
                EndWorldPosition = endWorldPosition;
                StartRotation = startRotation;
                EndRotation = endRotation;
                BirthTime = birthTime;
                Duration = duration;
                BaseScale = baseScale;
            }

            public Transform Transform { get; }
            public Vector3 StartWorldPosition { get; }
            public Vector3 EndWorldPosition { get; }
            public Quaternion StartRotation { get; }
            public Quaternion EndRotation { get; }
            public float BirthTime { get; }
            public float Duration { get; }
            public float BaseScale { get; }
        }

        private sealed class PooledFragment
        {
            public PooledFragment(
                GameObject gameObject,
                MeshRenderer renderer)
            {
                GameObject = gameObject;
                Renderer = renderer;
            }

            public GameObject GameObject { get; }
            public MeshRenderer Renderer { get; }
        }

        private readonly struct EffectVisual
        {
            public EffectVisual(Transform transform, MeshRenderer renderer)
            {
                Transform = transform;
                Renderer = renderer;
            }

            public Transform Transform { get; }
            public MeshRenderer Renderer { get; }
        }

        private sealed class EffectRig
        {
            public EffectRig(
                GameObject root,
                EffectVisual halo,
                EffectVisual ambientGlow,
                EffectVisual mouthFlash,
                EffectVisual[] rays,
                EffectVisual[] sparks)
            {
                Root = root;
                Halo = halo;
                AmbientGlow = ambientGlow;
                MouthFlash = mouthFlash;
                Rays = rays;
                Sparks = sparks;
            }

            public GameObject Root { get; }
            public EffectVisual Halo { get; }
            public EffectVisual AmbientGlow { get; }
            public EffectVisual MouthFlash { get; }
            public EffectVisual[] Rays { get; }
            public EffectVisual[] Sparks { get; }
        }
    }
}
