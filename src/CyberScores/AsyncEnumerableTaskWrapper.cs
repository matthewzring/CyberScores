#region License Header
/*
 * CyberScores - A Discord bot for interaction with the CyberPatriot scoreboard
 * Copyright (C) 2017-2024 Glen Husman, Matthew Ring, and contributors
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CyberScores;

public class AsyncEnumerableTaskWrapper<T> : IAsyncEnumerable<T>
{
    protected Task<IEnumerable<T>> EnumerableRetrieveTask { get; set; }

    public AsyncEnumerableTaskWrapper(Task<IEnumerable<T>> enumerableRetrieveTask)
    {
        EnumerableRetrieveTask = enumerableRetrieveTask ?? throw new ArgumentNullException(nameof(enumerableRetrieveTask));
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ctoken)
    {
        return new SyncEnumerableWrapper(ctoken, EnumerableRetrieveTask.ContinueWith(r => r.Result.GetEnumerator()));
    }

    class SyncEnumerableWrapper : IAsyncEnumerator<T>
    {
        // 
        public T Current => GetEnumerator.Result.Current;

        // this will be called FIRST in the use of the enumerator, so we can await the enumerator get task, the rest can use result safely
        public ValueTask<bool> MoveNextAsync()
        {
            if (CancellationToken.IsCancellationRequested)
            {
                return new ValueTask<bool>(Task.FromCanceled<bool>(CancellationToken));
            }
            return new ValueTask<bool>(GetEnumerator.ContinueWith<bool>(x => x.Result.MoveNext()));
        }

        Task<IEnumerator<T>> GetEnumerator { get; set; }
        CancellationToken CancellationToken { get; set; }

        public SyncEnumerableWrapper(CancellationToken ctoken, Task<IEnumerator<T>> getEnumerator)
        {
            GetEnumerator = getEnumerator;
            CancellationToken = ctoken;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        public ValueTask DisposeAsync()
        {
            if (disposedValue)
            {
                return default(ValueTask);
            }

            GetEnumerator.Result.Dispose();

            disposedValue = true;

            return default(ValueTask);
        }
        #endregion
    }
}
