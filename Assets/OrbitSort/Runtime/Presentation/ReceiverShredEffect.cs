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
            MarbleColor color,
            Vector3 startWorldPosition,
            Vector3 receiverWorldPosition,
            Vector3 outwardWorldDirection,
            Vector3 fullScale)
        {
            Marble = marble;
            Color = color;
            StartWorldPosition = startWorldPosition;
            ReceiverWorldPosition = receiverWorldPosition;
            OutwardWorldDirection = outwardWorldDirection;
            FullScale = fullScale;
        }

        public GameObject Marble { get; }
        public MarbleColor Color { get; }
        public Vector3 StartWorldPosition { get; }
        public Vector3 ReceiverWorldPosition { get; }
        public Vector3 OutwardWorldDirection { get; }
        public Vector3 FullScale { get; }
    }

    [DisallowMultipleComponent]
    public sealed class ReceiverShredEffect : MonoBehaviour
    {
        private const float IntakeDuration = 0.34f;
        private const float IntakeFinalScale = 0.06f;
        private const float BurstDuration = 0.46f;
        private const float MuzzleOffset = 0.18f;
        private const float FragmentScale = 0.82f;

        private static readonly float[] LateralSpread =
        {
            -0.46f,
            -0.30f,
            -0.15f,
            0f,
            0.15f,
            0.30f,
            0.46f
        };

        private static readonly float[] ForwardTravel =
        {
            0.68f,
            0.84f,
            1.02f,
            1.14f,
            1.00f,
            0.82f,
            0.66f
        };

        private static readonly float[] SpinDegrees =
        {
            -300f,
            250f,
            -360f,
            320f,
            -280f,
            350f,
            -240f
        };

        private readonly List<PooledFragment> _fragmentPool =
            new List<PooledFragment>();

        private Transform _dynamicRoot;
        private IReadOnlyDictionary<MarbleColor, Material> _materials;
        private Mesh _fragmentMesh;

        internal void Configure(
            Transform dynamicRoot,
            int maximumSimultaneousMarbles,
            IReadOnlyDictionary<MarbleColor, Material> materials)
        {
            _dynamicRoot = dynamicRoot;
            _materials = materials;
            _fragmentPool.Clear();
            EnsureFragmentMesh();
            CreateFragmentPool(
                maximumSimultaneousMarbles * LateralSpread.Length);
        }

        internal IEnumerator Animate(
            IReadOnlyList<ReceiverShredTarget> targets,
            Action onComplete)
        {
            float elapsed = 0f;
            while (elapsed < IntakeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / IntakeDuration);
                float gravityProgress = progress * progress * progress;
                float scale = Mathf.LerpUnclamped(
                    1f,
                    IntakeFinalScale,
                    SmootherStep(progress));

                foreach (ReceiverShredTarget target in targets)
                {
                    if (target.Marble == null)
                    {
                        continue;
                    }

                    Transform marble = target.Marble.transform;
                    marble.position = Vector3.LerpUnclamped(
                        target.StartWorldPosition,
                        target.ReceiverWorldPosition,
                        gravityProgress);
                    marble.localScale = target.FullScale * scale;
                }

                yield return null;
            }

            var activeFragments = new List<ActiveFragment>(
                targets.Count * LateralSpread.Length);
            foreach (ReceiverShredTarget target in targets)
            {
                if (target.Marble != null)
                {
                    target.Marble.SetActive(false);
                    ReleaseObject(target.Marble);
                }

                ActivateFragments(target, activeFragments);
            }

            elapsed = 0f;
            while (elapsed < BurstDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                foreach (ActiveFragment fragment in activeFragments)
                {
                    if (fragment.Transform == null)
                    {
                        continue;
                    }

                    float progress = Mathf.Clamp01(
                        (elapsed - fragment.Delay)
                        / (BurstDuration - fragment.Delay));
                    float inverse = 1f - progress;
                    float launchProgress =
                        1f - inverse * inverse * inverse;
                    float grow = SmootherStep(
                        Mathf.Clamp01(progress / 0.16f));
                    float shrink = 1f - SmootherStep(
                        Mathf.InverseLerp(0.28f, 1f, progress));

                    fragment.Transform.position = Vector3.LerpUnclamped(
                        fragment.StartWorldPosition,
                        fragment.EndWorldPosition,
                        launchProgress);
                    fragment.Transform.rotation = Quaternion.SlerpUnclamped(
                        fragment.StartRotation,
                        fragment.EndRotation,
                        launchProgress);
                    fragment.Transform.localScale =
                        Vector3.one
                        * (FragmentScale * grow * shrink);
                }

                yield return null;
            }

            foreach (ActiveFragment fragment in activeFragments)
            {
                if (fragment.Transform != null)
                {
                    fragment.Transform.gameObject.SetActive(false);
                }
            }

            onComplete?.Invoke();
        }

        private void ActivateFragments(
            ReceiverShredTarget target,
            List<ActiveFragment> activeFragments)
        {
            Vector2 radial = new Vector2(
                target.OutwardWorldDirection.x,
                target.OutwardWorldDirection.y).normalized;
            Vector2 tangent = new Vector2(-radial.y, radial.x);
            Vector3 muzzle = new Vector3(
                target.ReceiverWorldPosition.x + radial.x * MuzzleOffset,
                target.ReceiverWorldPosition.y + radial.y * MuzzleOffset,
                target.ReceiverWorldPosition.z - 0.08f);

            for (int index = 0; index < LateralSpread.Length; index++)
            {
                PooledFragment pooledFragment = GetAvailableFragment();
                if (pooledFragment == null)
                {
                    break;
                }

                GameObject fragmentObject = pooledFragment.GameObject;
                fragmentObject.name =
                    $"{MarbleColorUtility.DisplayName(target.Color)} "
                    + "Shred Fragment";
                fragmentObject.SetActive(true);
                fragmentObject.transform.position = muzzle;
                fragmentObject.transform.localScale = Vector3.zero;
                pooledFragment.Renderer.sharedMaterial =
                    _materials[target.Color];

                Vector3 endWorldPosition = muzzle
                    + new Vector3(
                        radial.x * ForwardTravel[index]
                        + tangent.x * LateralSpread[index],
                        radial.y * ForwardTravel[index]
                        + tangent.y * LateralSpread[index],
                        -0.04f);
                float startAngle = index * (360f / LateralSpread.Length);
                Quaternion startRotation = Quaternion.Euler(
                    18f + index * 7f,
                    12f + index * 9f,
                    startAngle);
                Quaternion endRotation = Quaternion.Euler(
                    54f + index * 11f,
                    42f + index * 13f,
                    startAngle + SpinDegrees[index]);

                fragmentObject.transform.rotation = startRotation;
                activeFragments.Add(
                    new ActiveFragment(
                        fragmentObject.transform,
                        muzzle,
                        endWorldPosition,
                        startRotation,
                        endRotation,
                        index * 0.010f));
            }
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
                meshFilter.sharedMesh = _fragmentMesh;
                MeshRenderer meshRenderer =
                    fragmentObject.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                fragmentObject.SetActive(false);
                _fragmentPool.Add(
                    new PooledFragment(fragmentObject, meshRenderer));
            }
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

        private void EnsureFragmentMesh()
        {
            if (_fragmentMesh != null)
            {
                return;
            }

            _fragmentMesh = new Mesh
            {
                name = "Receiver Shred Fragment"
            };
            _fragmentMesh.vertices = new[]
            {
                new Vector3(-0.16f, -0.10f, -0.07f),
                new Vector3(0.16f, -0.06f, -0.07f),
                new Vector3(-0.04f, 0.18f, -0.07f),
                new Vector3(-0.16f, -0.10f, 0.07f),
                new Vector3(0.16f, -0.06f, 0.07f),
                new Vector3(-0.04f, 0.18f, 0.07f)
            };
            _fragmentMesh.triangles = new[]
            {
                0, 2, 1,
                3, 4, 5,
                0, 1, 4,
                0, 4, 3,
                1, 2, 5,
                1, 5, 4,
                2, 0, 3,
                2, 3, 5
            };
            _fragmentMesh.RecalculateNormals();
            _fragmentMesh.RecalculateBounds();
            _fragmentMesh.UploadMeshData(true);
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
            if (_fragmentMesh != null)
            {
                ReleaseObject(_fragmentMesh);
                _fragmentMesh = null;
            }
        }

        private readonly struct ActiveFragment
        {
            public ActiveFragment(
                Transform transform,
                Vector3 startWorldPosition,
                Vector3 endWorldPosition,
                Quaternion startRotation,
                Quaternion endRotation,
                float delay)
            {
                Transform = transform;
                StartWorldPosition = startWorldPosition;
                EndWorldPosition = endWorldPosition;
                StartRotation = startRotation;
                EndRotation = endRotation;
                Delay = delay;
            }

            public Transform Transform { get; }
            public Vector3 StartWorldPosition { get; }
            public Vector3 EndWorldPosition { get; }
            public Quaternion StartRotation { get; }
            public Quaternion EndRotation { get; }
            public float Delay { get; }
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
    }
}
