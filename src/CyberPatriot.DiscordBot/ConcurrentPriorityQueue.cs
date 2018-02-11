using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using CyberPatriot.Models;
using System.Collections.Concurrent;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CyberPatriot.DiscordBot
{
    // lazy man's two-priority priority queue
    // Mostly(TM) threadsafe to avoid locks
    public class ConcurrentPriorityQueue<T> : IProducerConsumerCollection<T>
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

        void ICollection.CopyTo(Array array, int index)
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

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}