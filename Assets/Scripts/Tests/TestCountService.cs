#nullable enable

using UnityEngine;
using Zero.Services;
using Zero.Services.Base;

namespace Zero.Tests
{
    internal sealed class TestCountService : MonoBehaviour
    {
        private static readonly LazyService<ICountService> COUNT_SERVICE = new();

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _ = COUNT_SERVICE.Value;
            }
        }
    }
}