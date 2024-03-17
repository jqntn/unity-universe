using UnityEngine;

namespace Zero.Utils
{
    internal static class TimeUtils
    {
        public static float LastFrameTime => Time.deltaTime / 1_000.0f;
        public static int LastFrameTimeInt => Mathf.CeilToInt(LastFrameTime);
    }
}