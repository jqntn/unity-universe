#nullable enable

using UnityEngine;
using Zero.Services.Base;

namespace Zero.Services
{
    internal interface IWeatherService : IMonoService
    {
        Light? Sun { get; }
    }
}