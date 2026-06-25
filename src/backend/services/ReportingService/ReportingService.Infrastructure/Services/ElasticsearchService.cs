// ReportingService.Infrastructure / Services / ElasticsearchService.cs
//
// Elasticsearch entegrasyonu — Elastic.Clients.Elasticsearch 8.x
//
// Index: "transactions"
//   - TransactionId, CustomerId, MerchantId: keyword (UUID)
//   - MerchantTitle: text (full-text arama)
//   - Amount: double (range sorgu)
//   - Status: keyword
//   - OccurredAt: date (tarih filtresi, sıralama)

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Logging;
using ReportingService.Domain.Models;

namespace ReportingService.Infrastructure.Services;

public interface IElasticsearchService
{
    Task IndexTransactionAsync(TransactionDocument doc, CancellationToken ct = default);
    Task<IReadOnlyList<TransactionDocument>> GetByCustomerAsync(Guid customerId, int page = 1, int size = 20, CancellationToken ct = default);
    Task<IReadOnlyList<TransactionDocument>> SearchAsync(Guid? customerId, Guid? merchantId, DateTimeOffset? from, DateTimeOffset? to, int page = 1, int size = 20, CancellationToken ct = default);
}

public class ElasticsearchService(
    ElasticsearchClient client,
    ILogger<ElasticsearchService> logger) : IElasticsearchService
{
    private const string IndexName = "transactions";

    /// <summary>İşlem belgesini Elasticsearch'e yazar (UPSERT — aynı ID varsa günceller).</summary>
    public async Task IndexTransactionAsync(TransactionDocument doc, CancellationToken ct = default)
    {
        var response = await client.IndexAsync(doc, IndexName, doc.TransactionId.ToString(), ct);

        if (!response.IsValidResponse)
            logger.LogError("Elasticsearch index failed for {TransactionId}. Error: {Error}",
                doc.TransactionId, response.ElasticsearchServerError?.Error?.Reason);
    }

    /// <summary>Müşterinin son işlemlerini tarih bazlı sıralı getirir.</summary>
    public async Task<IReadOnlyList<TransactionDocument>> GetByCustomerAsync(
        Guid customerId, int page = 1, int size = 20, CancellationToken ct = default)
    {
        var response = await client.SearchAsync<TransactionDocument>(s => s
            .Index(IndexName)
            .From((page - 1) * size)
            .Size(size)
            .Query(q => q
                .Term(t => t
                    .Field(new Field("customerId"))
                    .Value(customerId.ToString())))
            .Sort(so => so.Field(new Field("occurredAt"), new FieldSort { Order = SortOrder.Desc })),
            ct);

        if (!response.IsValidResponse)
        {
            logger.LogError("Elasticsearch query failed for customer {CustomerId}", customerId);
            return [];
        }

        return response.Documents.ToList();
    }

    /// <summary>Çoklu filtreli arama (admin / merchant paneli).</summary>
    public async Task<IReadOnlyList<TransactionDocument>> SearchAsync(
        Guid? customerId, Guid? merchantId,
        DateTimeOffset? from, DateTimeOffset? to,
        int page = 1, int size = 20, CancellationToken ct = default)
    {
        var filters = new List<Query>();

        if (customerId.HasValue)
            filters.Add(new TermQuery(new Field("customerId")) { Value = customerId.Value.ToString() });

        if (merchantId.HasValue)
            filters.Add(new TermQuery(new Field("merchantId")) { Value = merchantId.Value.ToString() });

        if (from.HasValue || to.HasValue)
        {
            var rangeQuery = new DateRangeQuery(new Field("occurredAt"));
            if (from.HasValue) rangeQuery.Gte = from.Value.UtcDateTime;
            if (to.HasValue) rangeQuery.Lte = to.Value.UtcDateTime;
            filters.Add(rangeQuery);
        }

        var response = await client.SearchAsync<TransactionDocument>(s => s
            .Index(IndexName)
            .From((page - 1) * size)
            .Size(size)
            .Query(q => q.Bool(b => b.Filter(filters.ToArray())))
            .Sort(so => so.Field(new Field("occurredAt"), new FieldSort { Order = SortOrder.Desc })),
            ct);

        if (!response.IsValidResponse)
        {
            logger.LogError("Elasticsearch search failed");
            return [];
        }

        return response.Documents.ToList();
    }
}
