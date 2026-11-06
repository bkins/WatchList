using System.Net;

namespace WatchList.Import.Tests;

public sealed class CloudSyncRecordingHttpHandler : HttpMessageHandler
{
    public Queue<HttpResponseMessage> Responses { get; } = new();
    public List<(HttpMethod Method, Uri? Uri, string? FirebaseEtag, string? IfMatch)> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var firebaseEtag = request.Headers.TryGetValues("X-Firebase-ETag", out var firebaseValues) ? firebaseValues.Single() : null;
        var ifMatch      = request.Headers.TryGetValues("if-match", out var matchValues) ? matchValues.Single() : null;
        Requests.Add((request.Method, request.RequestUri, firebaseEtag, ifMatch));
        return Task.FromResult(Responses.Dequeue());
    }
}
