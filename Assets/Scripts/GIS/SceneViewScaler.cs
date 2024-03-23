using UnityEngine;

namespace Zero.GIS
{
    internal sealed class SceneViewScaler : MonoBehaviour
    {
        [Range(1e-09f, 1.0f)]
        [SerializeField] private float _scale = 1e-05f;

        private void Awake()
        {
            transform.localScale = Vector3.one;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            transform.localScale = Vector3.one * _scale;
        }
#endif
    }
}