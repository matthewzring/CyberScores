#region License Header
/*


    CyberPatriot Discord Bot - an unofficial tool to integrate the AFA CyberPatriot
    competition scoreboard with the Discord chat platform
    Copyright (C) 2017, 2018, 2019  Glen Husman and contributors

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.

*/
#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CyberPatriot.Services
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
        protected sealed class TimedTokenProvider<T>
        {
            private readonly IProducerConsumerCollection<TaskCompletionSource<T>> _waiting;
            private readonly ConcurrentQueue<T> _pushedAuthorizationTokens = new ConcurrentQueue<T>();
            private readonly object _syncContext = new object();
            private volatile ConcurrentBag<Task> _pulsePrereqs = new ConcurrentBag<Task>();
            private readonly int _maxQueuedAuthTokens;
            private readonly T _authorizationConstant;

            public TimedTokenProvider(T authConstant, int maxQueuedAuthTokens, IProducerConsumerCollection<TaskCompletionSource<T>> waitingCollection)
            {
                _authorizationConstant = authConstant;
                _maxQueuedAuthTokens = maxQueuedAuthTokens;
                _waiting = waitingCollection;
            }

            public TimedTokenProvider(T authConstant, int maxQueuedAuthTokens) : this(authConstant, maxQueuedAuthTokens, new ConcurrentQueue<TaskCompletionSource<T>>())
            {
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

                        // ContinueWith null result: we don't care about the results, and we explicitly want to ignore exceptions
                        // the important thing is that we await all of them
                        // avoid a "using System.Linq" so we don't accidentally do something not threadsafe in this file
                        Task.WhenAll(System.Linq.Enumerable.Select(prereqBag, tTmp => tTmp.ContinueWith(t => (object)null))).Wait();

                        prereqBag = null;

                        // finally complete a waiting task
                        TaskCompletionSource<T> tcs;
                        if (_waiting.TryTake(out tcs))
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
                if (!_waiting.TryAdd(tcs))
                {
                    throw new InvalidOperationException("Too many requests are enqueued, please wait before making another ratelimited request.");
                }
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

        protected TimedTokenProvider<byte> Awaiter { get; }
        private Timer Timer { get; }

        protected virtual IProducerConsumerCollection<TaskCompletionSource<byte>> GenerateDefaultQueue() => null;

        protected TimerRateLimitProvider(TimeSpan interval, int maxQueuedAuth, IProducerConsumerCollection<TaskCompletionSource<byte>> internalQueue)
        {
            Awaiter = (internalQueue = internalQueue ?? GenerateDefaultQueue()) == null ?
                new TimedTokenProvider<byte>(1, maxQueuedAuth) :
                new TimedTokenProvider<byte>(1, maxQueuedAuth, internalQueue);
            Awaiter.AddReadiedAuthTokens(maxQueuedAuth);
            Timer = new Timer(TriggerAwaiter, null, TimeSpan.Zero, interval);
        }

        public TimerRateLimitProvider(TimeSpan interval, int maxQueuedAuth) : this(interval, maxQueuedAuth, null)
        {
        }

        public TimerRateLimitProvider(int millis, int maxQueuedAuth) : this(TimeSpan.FromMilliseconds(millis), maxQueuedAuth)
        {
        }

        private void TriggerAwaiter(object state = null) => Awaiter.Pulse();

        public virtual Task GetWorkAuthorizationAsync() => Awaiter.Wait();

        public virtual void AddPrerequisite(Task prerequisite) => Awaiter.RegisterPrerequisite(prerequisite);

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

    public class PriorityTimerRateLimitProvider : TimerRateLimitProvider
    {
        // lazy man's two-priority priority queue
        // Mostly(TM) threadsafe to avoid locks
        protected class ConcurrentPriorityQueue<T> : IProducerConsumerCollection<T>
        {
            public ConcurrentQueue<T> HighPriority { get; } = new ConcurrentQueue<T>();
            public ConcurrentQueue<T> LowPriority { get; } = new ConcurrentQueue<T>();

            // threadsafety deviation: one collection may change after count is called before this returns => wrong result
            // for callers probably they need to do some handling of this case anyway
            public int Count => HighPriority.Count + LowPriority.Count;

            public bool IsSynchronized => false;

            public object SyncRoot => throw new InvalidOperationException();

            public void CopyTo(T[] array, int index)
            {
                // unsafe:
                // nonatomic, one of these could fail but not the other
                // if HighPriority changes after CopyTo before we call Count we could have problems
                HighPriority.CopyTo(array, index);
                LowPriority.CopyTo(array, index + HighPriority.Count);
            }

            void System.Collections.ICollection.CopyTo(Array array, int index)
            {
                // unsafe, see CopyTo(T[], int)
                (HighPriority as IProducerConsumerCollection<T>).CopyTo(array, index);
                (HighPriority as IProducerConsumerCollection<T>).CopyTo(array, index + HighPriority.Count);
            }

            // potentially unsafe
            public IEnumerator<T> GetEnumerator() => HighPriority.Concat(LowPriority).GetEnumerator();

            public T[] ToArray() => (this as IEnumerable<T>).ToArray();

            bool IProducerConsumerCollection<T>.TryAdd(T item)
            {
                AddHigh(item);
                return true;
            }

            public void AddHigh(T item) => HighPriority.Enqueue(item);
            public void AddLow(T item) => LowPriority.Enqueue(item);

            public bool TryTake(out T item)
            {
                // worst-case, which I'm not worried about for this use case:
                // we add to highPrio after tryDequeue
                // for our purposes (rate limiting) it doesn't really matter

                if (HighPriority.TryDequeue(out item))
                {
                    return true;
                }

                return LowPriority.TryDequeue(out item);
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public PriorityTimerRateLimitProvider(TimeSpan interval, int maxQueuedAuth) : base(interval, maxQueuedAuth)
        {
            LowPriorityRateLimiter = new LowPriorityWrapperRateLimitProvider(this);
        }

        public PriorityTimerRateLimitProvider(int millis, int maxQueuedAuth) : this(TimeSpan.FromMilliseconds(millis), maxQueuedAuth)
        {
        }

        protected ConcurrentPriorityQueue<TaskCompletionSource<byte>> Queue { get; } = new ConcurrentPriorityQueue<TaskCompletionSource<byte>>();

        protected override IProducerConsumerCollection<TaskCompletionSource<byte>> GenerateDefaultQueue() => Queue;

        public virtual Task GetLowPriorityWorkAuthorizationAsync()
        {
            // Low priority work differs in a few ways:
            // 1) high priority work is always completed first
            // 2) low priority work does not use queued-up auth tokens
            TaskCompletionSource<byte> tcs = new TaskCompletionSource<byte>();
            // this queue is used internally by our timer
            Queue.AddLow(tcs);
            return tcs.Task;
        }

        public IRateLimitProvider LowPriorityRateLimiter { get; }

        protected class LowPriorityWrapperRateLimitProvider : IRateLimitProvider
        {
            protected readonly PriorityTimerRateLimitProvider Parent;
            public LowPriorityWrapperRateLimitProvider(PriorityTimerRateLimitProvider parent)
            {
                Parent = parent;
            }

            public void AddPrerequisite(Task prereq) => Parent.AddPrerequisite(prereq);

            public Task GetWorkAuthorizationAsync() => Parent.GetLowPriorityWorkAuthorizationAsync();
        }
    }
}