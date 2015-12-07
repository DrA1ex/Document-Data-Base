using System;
using System.Threading;

namespace Common.Utils
{
    public static class SynchronizationContextUtils
    {
        public static void Post<T>(this SynchronizationContext ctx, Action<T> d, T state)
        {
            ctx.Post(o => d((T)o), state);
        }

        public static void Post(this SynchronizationContext ctx, Action action)
        {
            ctx.Post((o) => action(), null);
        }
    }
}
