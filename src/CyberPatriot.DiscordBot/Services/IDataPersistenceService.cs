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