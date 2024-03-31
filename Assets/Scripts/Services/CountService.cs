#nullable enable

using UnityEngine;

namespace Zero.Services
{
    internal sealed class CountService : ICountService
    {
        private static int _count;

        public CountService()
        {
            Debug.LogError(_count++);
        }
    }
}