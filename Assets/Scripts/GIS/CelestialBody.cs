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

        public CesiumGlobeAnchor GlobeAnchor { get; private set; }
        public Cesium3DTileset WorldTerrain { get; private set; }

        private void OnValidate()
        {
            SetScale();
        }

        private void Awake()
        {
            GlobeAnchor = GetComponent<CesiumGlobeAnchor>();
            WorldTerrain = GetComponentInChildren<Cesium3DTileset>();
        }

        private void Update()
        {
            CheckPlayerDistance();
        }

        private void SetScale()
        {
            if (WorldTerrain != null)
            {
                WorldTerrain.transform.localScale = Vector3.one * (float)(MaxRadius / CesiumWgs84Ellipsoid.GetMaximumRadius());
            }
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