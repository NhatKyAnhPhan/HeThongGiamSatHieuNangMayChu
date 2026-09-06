using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nest;
using Nhom3_NoSQL.Backend.Interfaces;
using Nhom3_NoSQL.Backend.Models;

namespace Nhom3_NoSQL.Database
{
    //Dùng thư viện NEST (ổn định trên .NET Framework 4.7.2) thay vì client Elastic mới
    public class ElasticSearchService : ISearchService
    {
        private readonly ElasticClient _client;
        private const string IndexName = "alert_events";

        public ElasticSearchService(string uri)
        {
            var settings = new ConnectionSettings(new Uri(uri))
                .DefaultIndex(IndexName);
            _client = new ElasticClient(settings);
        }

        public async Task IndexAlertAsync(AlertEvent alertEvent)
        {
            await _client.IndexAsync(alertEvent, i => i.Index(IndexName));
        }

        public async Task<List<AlertEvent>> SearchAlertsAsync(string keyword, DateTime? from = null, DateTime? to = null)
        {
            var response = await _client.SearchAsync<AlertEvent>(s => s
                .Index(IndexName)
                .Query(q => q
                    .Match(m => m
                        .Field(f => f.Message)
                        .Query(keyword)
                    )
                )
            );

            return response.Documents.ToList();
        }
    }
}
