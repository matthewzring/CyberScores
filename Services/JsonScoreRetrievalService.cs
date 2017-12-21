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

        public static string Serialize(CompleteScoreboardSummary summary,
            IDictionary<TeamId, ScoreboardDetails> teamDetails)
        {
            using (var memStr = new MemoryStream())
            using (var strWrite = new StreamWriter(memStr))
            using (var jsonWriter = new JsonTextWriter(strWrite))
            {
                // write
                jsonWriter.WriteStartObject();
                jsonWriter.WritePropertyName("summary");
                var serializer = JsonSerializer.CreateDefault();
                serializer.Serialize(jsonWriter, summary);
                jsonWriter.WritePropertyName("teams");
                serializer.Serialize(jsonWriter, teamDetails);
                jsonWriter.WriteEndObject();
                
                // read
                memStr.Position = 0;
                using (var strRead = new StreamReader(memStr))
                {
                    return strRead.ReadToEnd();
                }
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
                return Task.FromResult(summary);
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