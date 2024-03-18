using System;
using System.Threading.Tasks;

namespace Zero.Utils
{
    internal static class TaskUtils
    {
        public static Task WaitOneFrame => Task.Delay(TimeUtils.LastFrameTimeInt);
        public static Task WaitOneSecond => Task.Delay(1_000);

        public static async Task WaitUntil(Func<Task> wait, Func<bool> until)
        {
            while (!until.Invoke())
            {
                await wait.Invoke();
            }
        }

        public static async Task WaitEveryFrameUntil(Func<bool> until)
        {
            await WaitUntil(() => WaitOneFrame, until);
        }

        public static async Task WaitEverySecondUntil(Func<bool> until)
        {
            await WaitUntil(() => WaitOneSecond, until);
        }
    }
}