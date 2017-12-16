using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CyberPatriot.DiscordBot.Services
{
    public interface IDataPersistenceService
    {
        Task InitializeAsync(IServiceProvider provider);

        Task<TModel> FindOneAsync<TModel>(Expression<Func<TModel, bool>> predicate);
        IAsyncEnumerable<TModel> FindAllAsync<TModel>();
        IAsyncEnumerable<TModel> FindAllAsync<TModel>(Expression<Func<TModel, bool>> predicate);
        Task SaveAsync<TModel>(TModel model);
        Task<int> CountAsync<TModel>();
        Task<int> CountAsync<TModel>(Expression<Func<TModel, bool>> predicate);
        Task<bool> AnyAsync<TModel>();
        Task<bool> AnyAsync<TModel>(Expression<Func<TModel, bool>> predicate);
    }
}