using Zero.Controllers.Cameras;
using Zero.Services.Base;

namespace Zero.Services
{
    internal interface ICameraService : IMonoService
    {
        ICameraController CurrentCameraController { get; }

        void RegisterCameraController(in ICameraController cameraController);
    }
}