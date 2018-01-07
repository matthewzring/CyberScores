using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CyberPatriot.DiscordBot.Services
{
    public interface IRateLimitProvider
    {
        Task GetWorkAuthorizationAsync();
        void AddPrerequisite(Task prereq);
    }

    /// <summary>
    /// A rate limit provider that performs no limiting.
    /// </summary>
    public class NoneRateLimitProvider : IRateLimitProvider
    {
        public Task GetWorkAuthorizationAsync() => Task.CompletedTask;
        public void AddPrerequisite(Task t) { }
    }

    /// <summary>
    /// A rate limit provider backed by a Timer which only applies limits in cases of load.
    /// May have extended delays in the event of unmet preconditions.
    /// </summary>
    public class TimerRateLimitProvider : IRateLimitProvider, IDisposable
    {
        // based on https://stackoverflow.com/questions/34792699/async-version-of-monitor-pulse-wait
        internal sealed class TimedTokenProvider<T>
        {
            private readonly ConcurrentQueue<TaskCompletionSource<T>> _waiting = new ConcurrentQueue<TaskCompletionSource<T>>();
            private readonly ConcurrentQueue<T> _pushedAuthorizationTokens = new ConcurrentQueue<T>();
            private readonly object _syncContext = new object();
            private volatile ConcurrentBag<Task> _pulsePrereqs = new ConcurrentBag<Task>();
            private readonly int _maxQueuedAuthTokens;
            private readonly T _authorizationConstant;

            public TimedTokenProvider(T authConstant, int maxQueuedAuthTokens)
            {
                _authorizationConstant = authConstant;
                _maxQueuedAuthTokens = maxQueuedAuthTokens;
            }

            public void Pulse()
            {
                // this is called from a threadpool thread, we don't mind if we block it
                // but if it executes for a while we don't want it to spin up 1000 threads, therefore
                // only one thread can be trying to dequeue an awaiter at once
                // if another thread comes along while this is still in the lock, it'll silently "fail" and exit
                // in other words, get the lock if we can, let the next timer pass try again if we can't
                if (Monitor.TryEnter(_syncContext))
                {
                    try
                    {
                        // FIXME this feels dreadfully hacky
                        // should not need to recreate the bag every pulse
                        var prereqBag = new ConcurrentBag<Task>();
                        prereqBag = Interlocked.Exchange(ref _pulsePrereqs, prereqBag);

                        // this is the old prereq bag, nobody's modifying it (we've swapped it out)
                        // wait for all the prereqs, then clear the bag (we don't want a reference to them lying around)
                        // this is the blocking call that necessitates the lock
                        Task.WhenAll(prereqBag).Wait();
                        prereqBag.Clear();
                        prereqBag = null;

                        // finally complete a waiting task
                        TaskCompletionSource<T> tcs;
                        if (_waiting.TryDequeue(out tcs))
                        {
                            // SetResult - we're the only thread (as enforced by the lock) that's completing these tasks
                            // if something goes wrong with this call (it shouldn't) it should except and I should learn about it
                            tcs.SetResult(_authorizationConstant);
                        }
                        // we're the only thread pushing to this collection, as enforced by Monitor.TryEnter
                        // this means that even though there are 2 ops here (lengthCompare then enqueue), we won't go over the limit
                        // worst case someone pushes to "waiting" and instead of finishing that task we enqueue a work auth token
                        else if (_pushedAuthorizationTokens.Count < _maxQueuedAuthTokens)
                        {
                            _pushedAuthorizationTokens.Enqueue(_authorizationConstant);
                        }

                        // otherwise do nothing
                    }
                    finally
                    {
                        Monitor.Exit(_syncContext);
                    }
                }
            }

            public Task<T> Wait()
            {
                // in this model, no two awaiters can wait on the same task
                /*
                TaskCompletionSource<byte> tcs;
                if (_waiting.TryPeek(out tcs))
                {
                    return tcs.Task;
                }
                */

                // if there's a queued auth token, use that first
                if (_pushedAuthorizationTokens.TryDequeue(out T authToken))
                {
                    return Task.FromResult(authToken);
                }

                var tcs = new TaskCompletionSource<T>();
                _waiting.Enqueue(tcs);
                return tcs.Task;
            }

            public void RegisterPrerequisite(Task t)
            {
                // if we're in the middle of a tick it's ok if the prereq doesn't get awaited this tick
                // the lock isn't critical over here
                // it's just important it gets awaited before the next Pulse
                // this field is volatile, so even though we reassign the bag frequently, this should always add to the latest copy
                // that is, this prereq will always be awaited before assigning the next auth token
                _pulsePrereqs.Add(t);
            }

            internal void AddReadiedAuthTokens(int count)
            {
                if (count < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(count));
                }

                // this uses the same monitor calls as the write thread above, so only one can be executing at a time
                // although this is intended as an initialization method, this prevents undesired race conditions and ensures we won't exceed the limit
                lock (_syncContext)
                {
                    if (_pushedAuthorizationTokens.Count + count > _maxQueuedAuthTokens)
                    {
                        throw new InvalidOperationException("Adding the given number of auth tokens would exceed the maximum queue length for auth tokens.");
                    }

                    for (int i = 0; i < count; i++)
                    {
                        _pushedAuthorizationTokens.Enqueue(_authorizationConstant);
                    }
                }
            }
        }


        private TimedTokenProvider<byte> Awaiter { get; }
        private Timer Timer { get; }

        public TimerRateLimitProvider(TimeSpan interval, int maxQueuedAuth)
        {
            Awaiter = new TimedTokenProvider<byte>(1, maxQueuedAuth);
            Awaiter.AddReadiedAuthTokens(maxQueuedAuth);
            Timer = new Timer(TriggerAwaiter, null, TimeSpan.Zero, interval);
        }

        public TimerRateLimitProvider(int millis, int maxQueuedAuth) : this(TimeSpan.FromMilliseconds(millis), maxQueuedAuth)
        {
        }

        private void TriggerAwaiter(object state = null) => Awaiter.Pulse();

        public Task GetWorkAuthorizationAsync() => Awaiter.Wait();

        public void AddPrerequisite(Task prerequisite) => Awaiter.RegisterPrerequisite(prerequisite);

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Timer.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~TimerRateLimitProvider() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}