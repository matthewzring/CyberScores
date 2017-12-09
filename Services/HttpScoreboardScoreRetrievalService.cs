using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.Services
{
    public class HttpScoreboardScoreRetrievalService : IScoreRetrievalService
    {
        public string Hostname { get; }
        protected HttpClient Client { get; set; }

        public HttpScoreboardScoreRetrievalService(string hostname)
        {
            Hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
            Client = new HttpClient();
        }

        protected virtual Uri BuildScoreboardUri(Division? divisionFilter, string tierFilter)
        {

            var builder = new UriBuilder();
            builder.Scheme = "http";
            builder.Host = Hostname;
            builder.Path = "/";
            // there doesn't appear to be a terribly clean way to do this
            List<string> queryList = new List<string>(2);
            if (divisionFilter.HasValue)
            {
                queryList.Add("division=" + WebUtility.UrlEncode(divisionFilter.Value.ToStringCamelCaseToSpace()));
            }
            if (tierFilter != null)
            {

                queryList.Add("tier=" + WebUtility.UrlEncode(tierFilter));
            }

            builder.Query = string.Join("&", queryList);
            return builder.Uri;
        }

        protected virtual async Task<IEnumerable<ScoreboardSummary>> ProcessSummaries(Uri scoreboardUri)
        {
            
        }

        public async Task<ScoreboardDetails> GetDetails(TeamID team)
        {
            throw new System.NotImplementedException();
        }

        public IAsyncEnumerable<ScoreboardSummary> GetScoreboard()
        {
            return new AsyncSyncEnumerableWrapper<ScoreboardSummary>(ProcessSummaries(BuildScoreboardUri(null, null)));
        }

        public IAsyncEnumerable<ScoreboardSummary> GetScoreboard(Division divisionFilter)
        {
            return new AsyncSyncEnumerableWrapper<ScoreboardSummary>(ProcessSummaries(BuildScoreboardUri(divisionFilter, null)));
        }

        public IAsyncEnumerable<ScoreboardSummary> GetScoreboard(Division divisionFilter, string tierFilter)
        {
            return new AsyncSyncEnumerableWrapper<ScoreboardSummary>(ProcessSummaries(BuildScoreboardUri(divisionFilter, tierFilter)));
        }
    }
}