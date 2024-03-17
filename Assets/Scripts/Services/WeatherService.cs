using Unity;
using UnityEngine;
using Zero.Services.Base;

namespace Zero.Services
{
    internal sealed class WeatherService : MonoBehaviour, IWeatherService
    {
        [field: SerializeField] public Light Sun { get; private set; }

        public void Awake()
        {
            _ = ServiceLocator.Container.RegisterInstance<IWeatherService>(this);
        }

        public void OnDestroy()
        {
            ServiceLocator.Container.UnregisterInstance(this);
        }
    }
}