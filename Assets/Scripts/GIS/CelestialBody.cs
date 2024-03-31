#nullable enable

using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;
using Zero.Services;
using Zero.Services.Base;

namespace Zero.GIS
{
    [RequireComponent(typeof(CesiumGlobeAnchor))]
    internal sealed class CelestialBody : MonoBehaviour
    {
        private static readonly LazyService<ICameraService> CAMERA_SERVICE = new();

        [field: SerializeField] public double MaxRadius { get; private set; } = CesiumWgs84Ellipsoid.GetMaximumRadius();
        [field: SerializeField] public EReferenceUnit ReferenceUnit { get; private set; } = EReferenceUnit.M;
        [field: SerializeField] public ECelestialBodyType Type { get; private set; } = ECelestialBodyType.Planet;
        [field: SerializeField] public Transform? Surface { get; private set; }

        public CesiumGlobeAnchor GlobeAnchor { get; private set; } = null!;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Surface != null)
            {
                Surface.transform.localScale = Vector3.one * (float)(MaxRadius / CesiumWgs84Ellipsoid.GetMaximumRadius());
            }
        }
#endif

        private void Awake()
        {
            GlobeAnchor = GetComponent<CesiumGlobeAnchor>();
        }

        private void Update()
        {
            CheckPlayerDistance();
        }

        private void CheckPlayerDistance()
        {
            var cameraController = CAMERA_SERVICE.Value.CurrentCameraController;

            if (cameraController != null)
            {
                if (math.distancesq(transform.position, cameraController.Camera.transform.position) < MaxRadius * MaxRadius * 2.0)
                {
                    _ = cameraController.BodiesInRange.Add(this);
                }
                else
                {
                    _ = cameraController.BodiesInRange.Remove(this);
                }
            }
        }
    }
}