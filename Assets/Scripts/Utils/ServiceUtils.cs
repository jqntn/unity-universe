using System;
using System.Threading.Tasks;
using Unity;
using Zero.Services.Base;

namespace Zero.Utils
{
    internal static class ServiceUtils
    {
        public static async Task WaitForDependencies(params Type[] services)
        {
            foreach (var service in services)
            {
                if (service.GetInterface(nameof(IMonoService)) is null)
                {
                    throw new TypeAccessException($"{service.Name} is not a {nameof(IMonoService)}");
                }

                await TaskUtils.WaitEveryFrameUntil(() => ServiceLocator.Container.IsRegistered(service));
            }
        }

        public static async Task WaitForDependency<T>() where T : IMonoService
        {
            await TaskUtils.WaitEveryFrameUntil(() => ServiceLocator.Container.IsRegistered<T>());
        }
    }
}