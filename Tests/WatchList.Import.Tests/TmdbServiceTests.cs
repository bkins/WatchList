using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using WatchLists.Services;
using WatchLists.Services.Models;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class TmdbServiceTests
{
    [Theory]
    [InlineData("movie", 1)]
    [InlineData("tv", 2)]
    public async Task GetWatchProvidersAsync_WithTmdbResponse_PreservesNamedAvailability(string mediaPath
                                                                                     , int    expectedRequests)
    {
        const string json = """
            {
              "id": 42,
              "results": {
                "US": {
                  "link": "https://www.themoviedb.org/movie/42/watch?locale=US",
                  "flatrate": [{"provider_id":1,"provider_name":"Synthetic Stream","logo_path":"/stream.jpg"}],
                  "rent": [{"provider_id":2,"provider_name":"Synthetic Rent","logo_path":"/rent.jpg"}],
                  "buy": [{"provider_id":3,"provider_name":"Synthetic Buy","logo_path":"/buy.jpg"}]
                },
                "CA": {"rent": [{"provider_id":4,"provider_name":"Synthetic Canada"}]},
                "GB": {"buy": [{"provider_id":5,"provider_name":""}]}
              }
            }
            """;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync"
                                               , ItExpr.IsAny<HttpRequestMessage>()
                                               , ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync((HttpRequestMessage request, CancellationToken cancellationToken) =>
               {
                   var responseJson = request.RequestUri?.AbsolutePath == $"/3/{mediaPath}/42/watch/providers"
                       ? json
                       : """{"results":{}}""";
                   return new HttpResponseMessage(HttpStatusCode.OK)
                          {
                              Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                          };
               });
        using var client = new HttpClient(handler.Object);
        var service = new TmdbService(client, new ConfigurationBuilder().Build());

        var response = await service.GetWatchProvidersAsync(42);

        var results = Assert.IsType<Dictionary<string, CountryWatchProviders>>(response.Data?.Results);
        Assert.Equal(2, results.Count);
        var stream = Assert.Single(results["US"].Flatrate!);
        Assert.Equal("Synthetic Stream", stream.ProviderName);
        Assert.Equal(1, stream.ProviderId);
        Assert.Equal("/stream.jpg", stream.LogoPath);
        Assert.Equal("Synthetic Rent", Assert.Single(results["US"].Rent!).ProviderName);
        Assert.Equal("Synthetic Buy", Assert.Single(results["US"].Buy!).ProviderName);
        Assert.Equal("Synthetic Canada", Assert.Single(results["CA"].Rent!).ProviderName);
        handler.Protected()
               .Verify("SendAsync", Times.Exactly(expectedRequests)
                      , ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"results\":null}")]
    public async Task SearchMoviesAsync_AbsentProviderData_ReturnsNoDataDiagnostic(string json)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync"
                                               , ItExpr.IsAny<HttpRequestMessage>()
                                               , ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                                  {
                                      Content = new StringContent(json, Encoding.UTF8, "application/json")
                                  });
        using var client = new HttpClient(handler.Object);
        var configuration = new ConfigurationBuilder().Build();
        var service = new TmdbService(client, configuration);

        var result = await service.SearchMoviesAsync("Synthetic");

        Assert.Equal(string.Empty, service.ApiKey);
        Assert.Equal("No data returned.", result.Diagnostics[nameof(TmdbService)]);
        Assert.True(result.Data?.Results == null || result.Data.Results.Count == 0);
    }
}
