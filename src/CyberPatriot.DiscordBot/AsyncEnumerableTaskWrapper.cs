using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CyberPatriot.DiscordBot
{
    public class AsyncEnumerableTaskWrapper<T> : IAsyncEnumerable<T>
    {
        protected Task<IEnumerable<T>> EnumerableRetrieveTask { get; set; }

        public AsyncEnumerableTaskWrapper(Task<IEnumerable<T>> enumerableRetrieveTask)
        {
            EnumerableRetrieveTask = enumerableRetrieveTask ?? throw new ArgumentNullException(nameof(enumerableRetrieveTask));
        }

        public IAsyncEnumerator<T> GetEnumerator()
        {
            return new SyncEnumerableWrapper(EnumerableRetrieveTask.ContinueWith(r => r.Result.GetEnumerator()));
        }

        class SyncEnumerableWrapper : IAsyncEnumerator<T>
        {
            // 
            public T Current => GetEnumerator.Result.Current;

            // this will be called FIRST in the use of the enumerator, so we can await the enumerator get task, the rest can use result safely
            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                return (await GetEnumerator.ConfigureAwait(false)).MoveNext();
            }

            Task<IEnumerator<T>> GetEnumerator { get; set; }

            public SyncEnumerableWrapper(Task<IEnumerator<T>> getEnumerator)
            {
                GetEnumerator = getEnumerator;
            }

            #region IDisposable Support
            private bool disposedValue = false; // To detect redundant calls

            public void Dispose()
            {
                if (disposedValue)
                {
                    return;
                }

                GetEnumerator.Result.Dispose();

                disposedValue = true;
            }
            #endregion

        }
    }
}