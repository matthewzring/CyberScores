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
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using CyberPatriot.Models;
using LiteDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CyberPatriot.DiscordBot.Services
{
    public class LiteDbDataPersistenceService : IDataPersistenceService
    {
        public LiteDatabase Database { get; set; }

        protected virtual LiteCollection<TModel> GetCollection<TModel>() => Database.GetCollection<TModel>();

        public LiteDbDataPersistenceService()
        {

        }

        public LiteDbDataPersistenceService(LiteDatabase db)
        {
            Database = db;
        }

        public Task InitializeAsync(IServiceProvider provider)
        {
            if (Database == null)
            {
                Database = provider.GetService<LiteDatabase>();
                if (Database == null)
                {
                    var conf = provider.GetRequiredService<IConfiguration>();
                    Database = new LiteDatabase(conf["databaseFilename"]);
                }
            }

            // this is code specific to this program, the rest of this class is fairly generic
            // index each model
            GetCollection<Models.Guild>().EnsureIndex(g => g.Id, true);
            GetCollection<Models.User>().EnsureIndex(u => u.Id, true);

            Database.Mapper.RegisterType<TeamId>
            (
                serialize: (teamId) => teamId.ToString(),
                deserialize: (bson) => TeamId.Parse(bson.AsString)
            );

            return Task.CompletedTask;
        }

        public IDataPersistenceContext<TModel> OpenContext<TModel>(bool forWriting) where TModel : class
        {
            return new LiteDbPersistenceContext<TModel>(GetCollection<TModel>(), forWriting);
        }

        class LiteDbPersistenceContext<TModel> : IDataPersistenceContext<TModel> where TModel : class
        {
            public LiteDbPersistenceContext(LiteCollection<TModel> modelCollection, bool write)
            {
                Collection = modelCollection;
                if (write)
                {
                    toWrite = new HashSet<TModel>();
                }
            }

            private LiteCollection<TModel> Collection { get; }

            private ISet<TModel> toWrite;

            public Task<bool> AnyAsync()
            {
                // hacky
                return Task.FromResult(Collection.Exists(m => true));
            }

            public Task<bool> AnyAsync(Expression<Func<TModel, bool>> predicate)
            {
                return Task.FromResult(Collection.Exists(predicate));
            }

            public Task<int> CountAsync(Expression<Func<TModel, bool>> predicate)
            {
                return Task.FromResult(Collection.Count(predicate));
            }

            public Task<int> CountAsync()
            {
                return Task.FromResult(Collection.Count());
            }

            public IAsyncEnumerable<TModel> FindAllAsync()
            {
                var all = Collection.FindAll().ToList();
                toWrite?.AddAll(all);
                return all.ToAsyncEnumerable();
            }

            public IAsyncEnumerable<TModel> FindAllAsync(Expression<Func<TModel, bool>> predicate)
            {
                var all = Collection.Find(predicate).ToList();
                toWrite?.AddAll(all);
                return all.ToAsyncEnumerable();
            }

            private TModel FindOne(Expression<Func<TModel, bool>> predicate)
            {
                var single = Collection.FindOne(predicate);
                if (toWrite != null && single != null)
                {
                    toWrite.Add(single);
                }
                return single;
            }

            private TModel InvokeFactory(Func<TModel> factory)
            {
                var val = factory();
                toWrite?.Add(val);
                return val;
            }

            private async Task<TModel> InvokeFactoryAsync(Func<Task<TModel>> asyncFactory)
            {
                var val = await asyncFactory().ConfigureAwait(false);
                toWrite?.Add(val);
                return val;
            }

            public Task<TModel> FindOneAsync(Expression<Func<TModel, bool>> predicate) => Task.FromResult(FindOne(predicate));

            public Task<TModel> FindOneOrNewAsync(Expression<Func<TModel, bool>> predicate, Func<TModel> factory)
                => Task.FromResult(FindOne(predicate) ?? InvokeFactory(factory));

            public async Task<TModel> FindOneOrNewAsync(Expression<Func<TModel, bool>> predicate,
                Func<Task<TModel>> asyncFactory)
                => FindOne(predicate) ?? await InvokeFactoryAsync(asyncFactory).ConfigureAwait(false);


            public Task SaveAsync(TModel model)
            {
                Collection.Upsert(model);
                toWrite?.Remove(model);
                return Task.CompletedTask;
            }

            public Task WriteAsync()
            {
                Dispose();
                return Task.CompletedTask;
            }

            public void Dispose()
            {
                if (toWrite != null)
                {
                    // on context close, write everything we've returned if we're in write mode
                    foreach (var model in toWrite)
                    {
                        Collection.Upsert(model);
                    }
                    toWrite.Clear();
                }
            }

            public Task<int> DeleteAsync(Expression<Func<TModel, bool>> predicate)
            {
                try
                {
                    return Task.FromResult(Collection.Delete(predicate));
                } catch (Exception ex)
                {
                    return Task.FromException<int>(ex);
                }
            }
        }
    }
}