using System.Collections.Generic;
using UnityEngine;
using Zero.GIS;
using Zero.Services;
using Zero.Services.Base;
using Zero.Utils;

namespace Zero.Controllers.Cameras
{
    [RequireComponent(typeof(Camera))]
    internal sealed class SpectatorCameraController : MonoBehaviour, ICameraController
    {
        private static readonly LazyService<ICameraService> CAMERA_SERVICE = new();

        public Camera Camera { get; private set; }

        public HashSet<CelestialBody> BodiesInRange { get; }

        private async void Awake()
        {
            Camera = GetComponent<Camera>();

            await ServiceUtils.WaitForDependency<ICameraService>();

            CAMERA_SERVICE.Value.RegisterCameraController(this);
        }
    }
}