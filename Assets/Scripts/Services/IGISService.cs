#nullable enable

using System.Collections.Generic;
using Zero.GIS;
using Zero.Services.Base;

namespace Zero.Services
{
    internal interface IGISService : IMonoService
    {
        List<CelestialBody> CelestialBodies { get; }
    }
}