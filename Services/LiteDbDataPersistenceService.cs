using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.Extensions.DependencyInjection;

namespace CyberPatriot.DiscordBot.Services
{
    public class LiteDbDataPersistenceService : IDataPersistenceService
    {
        public LiteDatabase Database { get; set; }

        protected virtual LiteCollection<TModel> GetCollection<TModel>() => Database.GetCollection<TModel>();

        public LiteDbDataPersistenceService(LiteDatabase db)
        {
            Database = db;
        }

        public Task InitializeAsync(IServiceProvider provider)
        {
            if (Database == null)
            {
                Database = provider.GetService<LiteDatabase>();
            }

            // this is code specific to this program, the rest of this class is fairly generic
            // index each model
            GetCollection<Models.Guild>().EnsureIndex(g => g.Id, true);

            return Task.CompletedTask;
        }

        public Task<bool> AnyAsync<TModel>()
        {
            // hacky
            return Task.FromResult(GetCollection<TModel>().Exists(m => true));
        }

        public Task<bool> AnyAsync<TModel>(Expression<Func<TModel, bool>> predicate)
        {
            return Task.FromResult(GetCollection<TModel>().Exists(predicate));
        }

        public Task<int> CountAsync<TModel>(Expression<Func<TModel, bool>> predicate)
        {
            return Task.FromResult(GetCollection<TModel>().Count(predicate));
        }

        public Task<int> CountAsync<TModel>()
        {
            return Task.FromResult(GetCollection<TModel>().Count());
        }

        public IAsyncEnumerable<TModel> FindAllAsync<TModel>()
        {
            return AsyncEnumerable.ToAsyncEnumerable(GetCollection<TModel>().FindAll());
        }

        public IAsyncEnumerable<TModel> FindAllAsync<TModel>(Expression<Func<TModel, bool>> predicate)
        {
            return AsyncEnumerable.ToAsyncEnumerable(GetCollection<TModel>().Find(predicate));
        }

        public Task<TModel> FindOneAsync<TModel>(Expression<Func<TModel, bool>> predicate)
        {
            return Task.FromResult(GetCollection<TModel>().FindOne(predicate));
        }

        public Task SaveAsync<TModel>(TModel model)
        {
            GetCollection<TModel>().Upsert(model);
            return Task.CompletedTask;
        }
    }
}