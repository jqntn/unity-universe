#nullable enable

using System.Collections;
using Unity;
using UnityEngine;
using Zero.Controllers.Cameras;
using Zero.Services.Base;

namespace Zero.Services
{
    internal sealed class CameraService : MonoBehaviour, ICameraService
    {
        public ICameraController? CurrentCameraController { get; private set; }

        [SerializeField] private Camera _defaultCamera = null!;

        public void Awake()
        {
            _ = ServiceLocator.Container.RegisterInstance<ICameraService>(this);

            StartCoroutine(InstantiateDefaultCamera());
        }

        public void OnDestroy()
        {
            ServiceLocator.Container.UnregisterInstance(this);
        }

        public void RegisterCameraController(in ICameraController cameraController)
        {
            if (CurrentCameraController != null)
            {
                CurrentCameraController.Camera.tag = "Untagged";
            }

            CurrentCameraController = cameraController;

            CurrentCameraController.Camera.tag = "MainCamera";
        }

        private IEnumerator InstantiateDefaultCamera()
        {
            yield return new WaitForEndOfFrame();

            if (CurrentCameraController == null)
            {
                Instantiate(_defaultCamera, transform).tag = "MainCamera";
            }
        }
    }
}