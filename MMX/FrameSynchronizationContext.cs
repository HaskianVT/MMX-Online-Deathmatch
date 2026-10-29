using System;
using System.Collections.Generic;
using System.Threading;

namespace MMXOnline
{
    // Runs the continuations of awaited tasks at the start of a frame, on the game loop's thread.
    // This lets menus await network requests without blocking the game loop (required in the browser, which can't block)
    // and without game state being changed from another thread while a frame runs.
    public class FrameSynchronizationContext : SynchronizationContext
    {
        readonly Queue<(SendOrPostCallback callback, object state)> pending = new Queue<(SendOrPostCallback, object)>();

        public override void Post(SendOrPostCallback callback, object state)
        {
            lock (pending)
            {
                pending.Enqueue((callback, state));
            }
        }

        public override void Send(SendOrPostCallback callback, object state)
        {
            throw new NotSupportedException("Synchronous Send isn't supported; use Post.");
        }

        // Called once per frame by the game loop
        public void runPending()
        {
            while (true)
            {
                (SendOrPostCallback callback, object state) item;
                lock (pending)
                {
                    if (pending.Count == 0) return;
                    item = pending.Dequeue();
                }
                item.callback(item.state);
            }
        }
    }
}
