using UnityEngine;
using Zero.Services.Base;

namespace Zero.Services
{
    internal interface ICameraService : IMonoService
    {
        Camera MainCamera { get; }
    }
}