#nullable enable

namespace Zero.Services.Base
{
    internal interface IMonoService : IService
    {
        void Awake();
        void OnDestroy();
    }
}