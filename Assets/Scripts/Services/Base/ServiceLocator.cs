#nullable enable

using System;
using System.Linq;
using Unity;

namespace Zero.Services.Base
{
    internal static class ServiceLocator
    {
        public static IUnityContainer Container => _container.Value;

        private static readonly Lazy<IUnityContainer> _container = new(() =>
        {
            var container = new UnityContainer();

            RegisterSingletons(container);

            return container;
        });

        private static void RegisterSingletons(in IUnityContainer container)
        {
            container.RegisterSingleton<ICountService, CountService>();
        }

        public static void UnregisterInstance(this IUnityContainer container, IService instance)
        {
            container.Registrations.First(x => x.MappedToType == instance.GetType()).LifetimeManager.RemoveValue();
        }
    }
}