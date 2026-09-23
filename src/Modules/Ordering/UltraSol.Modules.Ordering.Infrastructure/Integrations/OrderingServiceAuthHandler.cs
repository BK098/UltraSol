using System.Net;
using System.Net.Http.Headers;

namespace UltraSol.Modules.Ordering.Infrastructure.Integrations;

public sealed class OrderingServiceAuthHandler(OrderingServiceToken tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await tokens.GetAsync(null, ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var replay = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            replay.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        if (request.Content is not null)
        {
            replay.Content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(ct));
            foreach (var header in request.Content.Headers)
            {
                replay.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
        var response = await base.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }
        response.Dispose();
        replay.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetAsync(token, ct));
        return await base.SendAsync(replay, ct);
    }
}