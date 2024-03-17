using System;
using Unity;

namespace Zero.Services.Base
{
    internal class LazyService<T>
    {
        public T Value => _lazy.Value;

        private readonly Lazy<T> _lazy;

        public LazyService()
        {
            _lazy = new(() => ServiceLocator.Container.Resolve<T>());
        }
    }
}