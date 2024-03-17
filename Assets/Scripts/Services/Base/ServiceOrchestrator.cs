using System.Collections.Generic;
using UnityEngine;

namespace Zero.Services.Base
{
    internal sealed class ServiceOrchestrator : MonoBehaviour
    {
        [SerializeField] private List<GameObject> _services;

        private void Awake()
        {
            foreach (var service in _services)
            {
                Instantiate(service, transform).name = service.name;
            }
        }
    }
}