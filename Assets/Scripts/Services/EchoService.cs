using Unity;
using UnityEngine;
using Zero.Services.Base;

namespace Zero.Services
{
    internal sealed class EchoService : MonoBehaviour, IEchoService
    {
        public void Awake()
        {
            _ = ServiceLocator.Container.RegisterInstance<IEchoService>(this);
        }

        public void OnDestroy()
        {
            ServiceLocator.Container.UnregisterInstance(this);
        }

        public void Echo()
        {
            Debug.LogError(nameof(EchoService));
        }
    }
}