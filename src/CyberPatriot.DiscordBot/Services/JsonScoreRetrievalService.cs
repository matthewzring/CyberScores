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

        public IReadOnlyDictionary<TeamId, ScoreboardDetails> StoredTeamDetails => teamDetails;

        public CompetitionRound Round { get; protected set; }
        Models.IScoreRetrieverMetadata IScoreRetrievalService.Metadata => Metadata;
        protected Models.ScoreRetrieverMetadata Metadata { get; set; } = new Models.ScoreRetrieverMetadata()
        {
            IsDynamic = false,
            StaticSummaryLine = "CCS Archive",
            FormattingOptions = new ScoreFormattingOptions()
        };

        public JsonScoreRetrievalService()
        {
        }

        public async Task InitializeAsync(IServiceProvider provider, Microsoft.Extensions.Configuration.IConfigurationSection config)
        {
            Deserialize(await File.ReadAllTextAsync(config["source"]).ConfigureAwait(false));
        }

        public static async Task<string> SerializeAsync(CompleteScoreboardSummary summary,
            IDictionary<TeamId, ScoreboardDetails> teamDetails, CompetitionRound round = 0)
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
                    await SerializeAsync(sw, summary, teamDetails, round).ConfigureAwait(false);

                    // read
                    memStr.Position = 0;
                    return await sr.ReadToEndAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                sw?.Dispose();
                sr?.Dispose();
            }
        }

        public static async Task SerializeAsync(TextWriter target, CompleteScoreboardSummary summary,
            IDictionary<TeamId, ScoreboardDetails> teamDetails, CompetitionRound round = 0)
        {
            using (JsonWriter jw = new JsonTextWriter(target))
            {
                jw.CloseOutput = false;

                // write
                await jw.WriteStartObjectAsync().ConfigureAwait(false);
                await jw.WritePropertyNameAsync("summary").ConfigureAwait(false);
                var serializer = JsonSerializer.CreateDefault();
                // serialize
                serializer.Serialize(jw, summary);
                await jw.WritePropertyNameAsync("teams").ConfigureAwait(false);
                serializer.Serialize(jw, teamDetails);
                await jw.WritePropertyNameAsync("round").ConfigureAwait(false);
                await jw.WriteValueAsync((int)round).ConfigureAwait(false);
                await jw.WriteEndObjectAsync().ConfigureAwait(false);
                await jw.FlushAsync().ConfigureAwait(false);
            }
        }

        public void Deserialize(string rawJson)
        {
            deserializedJsonLock.EnterWriteLock();
            try
            {
                JObject obj = JObject.Parse(rawJson);
                summary = obj["summary"].ToObject<CompleteScoreboardSummary>();
                teamDetails = obj["teams"].ToObject<Dictionary<TeamId, ScoreboardDetails>>();

                // workaround, see #18
                summary.SnapshotTimestamp = summary.SnapshotTimestamp.ToUniversalTime();
                foreach (var teamData in teamDetails.Values)
                {
                    teamData.SnapshotTimestamp = teamData.SnapshotTimestamp.ToUniversalTime();
                }

                try
                {
                    Round = (CompetitionRound)obj["round"].Value<int>();
                }
                catch
                {
                    Round = 0;
                }
                Metadata.StaticSummaryLine = "CCS Archive" + (Round == 0 ? string.Empty : (", " + Round.ToStringCamelCaseToSpace()));
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