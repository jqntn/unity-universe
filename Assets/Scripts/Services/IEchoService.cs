#nullable enable

using Zero.Services.Base;

namespace Zero.Services
{
    internal interface IEchoService : IMonoService
    {
        void Echo();
    }
}