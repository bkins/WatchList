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
