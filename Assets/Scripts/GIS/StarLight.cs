#nullable enable

using UnityEngine;
using Zero.Services;
using Zero.Services.Base;

namespace Zero.GIS
{
    [RequireComponent(typeof(Light))]
    internal sealed class StarLight : MonoBehaviour
    {
        private static readonly LazyService<ICameraService> CAMERA_SERVICE = new();

        private Light _light = null!;

#if UNITY_EDITOR
        private void OnValidate()
        {
            Debug.Assert(GetComponent<Light>().type == LightType.Directional);
        }
#endif

        private void Awake()
        {
            _light = GetComponent<Light>();
        }

        private void Update()
        {
            var cameraController = CAMERA_SERVICE.Value.CurrentCameraController;

            if (cameraController != null)
            {
                _light.transform.LookAt(cameraController.Camera.transform);
            }
        }
    }
}