using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CyberPatriot.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CyberPatriot.DiscordBot.Services
{
    public class JsonScoreRetrievalService : IScoreRetrievalService, IDisposable
    {
        protected readonly ReaderWriterLockSlim deserializedJsonLock = new ReaderWriterLockSlim();
        protected CompleteScoreboardSummary summary;
        protected Dictionary<TeamId, ScoreboardDetails> teamDetails;

        public JsonScoreRetrievalService(string jsonContents)
        {
            Deserialize(jsonContents);
        }

        Task IScoreRetrievalService.InitializeAsync(IServiceProvider provider) => Task.CompletedTask;

        public static async Task<string> SerializeAsync(CompleteScoreboardSummary summary,
            IDictionary<TeamId, ScoreboardDetails> teamDetails)
        {
            StreamWriter sw = null;
            StreamReader sr = null;

            try
            {
                using (var memStr = new MemoryStream())
                {
                    sw = new StreamWriter(memStr);
                    sr = new StreamReader(memStr);

                    // write
                    await SerializeAsync(sw, summary, teamDetails);

                    // read
                    memStr.Position = 0;
                    return await sr.ReadToEndAsync();
                }
            }
            finally
            {
                sw?.Dispose();
                sr?.Dispose();
            }
        }

        public static async Task SerializeAsync(TextWriter target, CompleteScoreboardSummary summary,
            IDictionary<TeamId, ScoreboardDetails> teamDetails)
        {
            using (JsonWriter jw = new JsonTextWriter(target))
            {
                jw.CloseOutput = false;

                // write
                await jw.WriteStartObjectAsync();
                await jw.WritePropertyNameAsync("summary");
                var serializer = JsonSerializer.CreateDefault();
                // serialize
                await Task.Run(() => serializer.Serialize(jw, summary));
                await jw.WritePropertyNameAsync("teams");
                await Task.Run(() => serializer.Serialize(jw, teamDetails));
                await jw.WriteEndObjectAsync();
                await jw.FlushAsync();
            }
        }

        public void Deserialize(string rawJson)
        {
            deserializedJsonLock.EnterWriteLock();
            try
            {
                JObject obj = JObject.Parse(rawJson);
                summary = obj["summary"].Value<CompleteScoreboardSummary>();
                teamDetails = obj["teams"].Value<Dictionary<TeamId, ScoreboardDetails>>();
            }
            finally
            {
                deserializedJsonLock.ExitWriteLock();
            }
        }

        public Task<CompleteScoreboardSummary> GetScoreboardAsync(ScoreboardFilterInfo filter)
        {
            deserializedJsonLock.EnterReadLock();
            try
            {
                return Task.FromResult(summary.Clone().WithFilter(filter));
            }
            finally
            {
                deserializedJsonLock.ExitReadLock();
            }
        }

        public Task<ScoreboardDetails> GetDetailsAsync(TeamId team)
        {
            deserializedJsonLock.EnterReadLock();
            try
            {
                if (!teamDetails.TryGetValue(team, out ScoreboardDetails retVal) || retVal == null)
                {
                    throw new ArgumentException("The given team does not exist.");
                }

                return Task.FromResult(retVal);
            }
            finally
            {
                deserializedJsonLock.ExitReadLock();
            }
        }

        public void Dispose()
        {
            deserializedJsonLock?.Dispose();
        }
    }
}