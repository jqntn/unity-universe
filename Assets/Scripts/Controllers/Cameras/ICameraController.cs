#nullable enable

using System.Collections.Generic;
using UnityEngine;
using Zero.GIS;

namespace Zero.Controllers.Cameras
{
    internal interface ICameraController
    {
        Camera Camera { get; }

        HashSet<CelestialBody> BodiesInRange { get; }
    }
}