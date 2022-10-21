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
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CyberPatriot.DiscordBot.Services
{
    public interface IDataPersistenceService
    {
        Task InitializeAsync(IServiceProvider provider);

        IDataPersistenceContext<TModel> OpenContext<TModel>(bool forWriting) where TModel : class;
    }

    public interface IDataPersistenceContext<TModel> : IDisposable where TModel : class
    {
        Task<TModel> FindOneAsync(Expression<Func<TModel, bool>> predicate);
        Task<TModel> FindOneOrNewAsync(Expression<Func<TModel, bool>> predicate, Func<TModel> factory);
        Task<TModel> FindOneOrNewAsync(Expression<Func<TModel, bool>> predicate, Func<Task<TModel>> asyncFactory);
        /// <summary>
        /// Deletes rows matching the given predicate.
        /// </summary>
        /// <returns>The number of rows deleted.</returns>
        Task<int> DeleteAsync(Expression<Func<TModel, bool>> predicate);
        IAsyncEnumerable<TModel> FindAllAsync();
        IAsyncEnumerable<TModel> FindAllAsync(Expression<Func<TModel, bool>> predicate);
        Task SaveAsync(TModel model);
        Task<int> CountAsync();
        Task<int> CountAsync(Expression<Func<TModel, bool>> predicate);
        Task<bool> AnyAsync();
        Task<bool> AnyAsync(Expression<Func<TModel, bool>> predicate);

        // FIXME: this is intended as a DisposeAsync-style method, which duplicates the work of Dispose
        // LiteDb doesn't use Tasks internally, but for a backend which does, how is this impacted?
        Task WriteAsync();
    }
}