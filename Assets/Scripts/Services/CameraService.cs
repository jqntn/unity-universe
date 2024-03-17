using Unity;
using UnityEngine;
using Zero.Services.Base;

namespace Zero.Services
{
    internal sealed class CameraService : MonoBehaviour, ICameraService
    {
        [field: SerializeField] public Camera MainCamera { get; private set; }

        public void Awake()
        {
            _ = ServiceLocator.Container.RegisterInstance<ICameraService>(this);
        }

        public void OnDestroy()
        {
            ServiceLocator.Container.UnregisterInstance(this);
        }
    }
}