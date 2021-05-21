using System;

namespace Photon.Hive.Plugin
{
    public static class EnqueueStatus
    {
        public const int Success = 0;
        public const int Closed = 1;
    }

    public interface IPluginFiber
    {
        int Enqueue(Action action);
        object CreateTimer(Action action, int firstInMs, int regularInMs);
        object CreateOneTimeTimer(Action action, long firstInMs);
        void StopTimer(object timer);
    }
}
