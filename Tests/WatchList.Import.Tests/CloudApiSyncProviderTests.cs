using System.Net;
using WatchLists.Services;
using WatchLists.Services.Models;
using WatchLists.Services.MuseImport;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class CloudApiSyncProviderTests
{
    [Fact]
    public async Task ConditionalSync_Firebase_UsesReadRevisionAndReportsConflict()
    {
        var handler = new CloudSyncRecordingHttpHandler();
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"SchemaVersion\":2}") };
        response.Headers.TryAddWithoutValidation("ETag", "\"synthetic-revision\"");
        handler.Responses.Enqueue(response);
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.PreconditionFailed));
        var provider = new CloudApiSyncProvider(new HttpClient(handler));

        var read = await provider.FetchConditionalAsync("https://example.firebaseio.com", "synthetic");
        var result = await provider.UploadConditionalAsync("https://example.firebaseio.com", "synthetic", read!.Bundle!, read.Etag);

        Assert.Equal("true", handler.Requests[0].FirebaseEtag);
        Assert.Equal("https://example.firebaseio.com/watchlist/synthetic.json", handler.Requests[0].Uri!.ToString());
        Assert.Equal("\"synthetic-revision\"", handler.Requests[1].IfMatch);
        Assert.Equal(CloudSyncWriteResult.Conflict, result);
    }

    [Fact]
    public async Task ConditionalSync_MissingEtagOrUnsupportedProvider_FailsClosed()
    {
        var handler = new CloudSyncRecordingHttpHandler();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"SchemaVersion\":2}") });
        var provider = new CloudApiSyncProvider(new HttpClient(handler));

        var noRevision = await provider.FetchConditionalAsync("https://example.firebaseio.com", "synthetic");
        var unsupported = await provider.UploadConditionalAsync("https://jsonblob.com/api/jsonBlob/test", "synthetic", new SyncBundle(), "revision");

        Assert.Null(noRevision);
        Assert.Equal(CloudSyncWriteResult.Failed, unsupported);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ConditionalRead_EmptyDestination_ReturnsRevisionForCreation()
    {
        var handler = new CloudSyncRecordingHttpHandler();
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null") };
        response.Headers.TryAddWithoutValidation("ETag", "null_etag");
        handler.Responses.Enqueue(response);
        var provider = new CloudApiSyncProvider(new HttpClient(handler));

        var read = await provider.FetchConditionalAsync("https://example.firebaseio.com", "new-synthetic-destination");

        Assert.NotNull(read);
        Assert.Null(read.Bundle);
        Assert.Equal("null_etag", read.Etag);
    }
}
