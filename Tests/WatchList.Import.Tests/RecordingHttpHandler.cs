using System.Net;

namespace WatchList.Import.Tests;

public sealed class RecordingHttpHandler : HttpMessageHandler
{
    public Uri? RequestedUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestedUri = request.RequestUri;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                               {
                                   Content = new StringContent("[]")
                               });
    }
}
