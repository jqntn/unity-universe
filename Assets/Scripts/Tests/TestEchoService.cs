#nullable enable

using UnityEngine;
using Zero.Services;
using Zero.Services.Base;
using Zero.Utils;

namespace Zero.Tests
{
    internal sealed class TestEchoService : MonoBehaviour
    {
        private static readonly LazyService<IEchoService> MONO_SERVICE = new();

        private async void Awake()
        {
            await ServiceUtils.WaitForDependency<IEchoService>();

            MONO_SERVICE.Value.Echo();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                MONO_SERVICE.Value.Echo();
            }
        }
    }
}