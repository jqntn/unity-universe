#nullable enable

using System.Collections.Generic;
using Unity;
using UnityEngine;
using Zero.GIS;
using Zero.Services.Base;

namespace Zero.Services
{
    internal sealed class GISService : MonoBehaviour, IGISService
    {
        public List<CelestialBody> CelestialBodies { get; private set; } = new();

        public void Awake()
        {
            _ = ServiceLocator.Container.RegisterInstance<IGISService>(this);
        }

        public void OnDestroy()
        {
            ServiceLocator.Container.UnregisterInstance(this);
        }
    }
}