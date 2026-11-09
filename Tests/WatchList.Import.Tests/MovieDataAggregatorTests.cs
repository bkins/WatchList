using System.Net;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using WatchLists.DataAccess.Interfaces;
using WatchLists.Services;
using WatchLists.Services.Models;
using Xunit;

namespace WatchList.Import.Tests;

public sealed class MovieDataAggregatorTests
{
    [Fact]
    public async Task SearchMoviesAsync_RemakesFromTmdb_PreservesIdsYearsAndAvailability()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync"
                                               , ItExpr.IsAny<HttpRequestMessage>()
                                               , ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync((HttpRequestMessage request, CancellationToken cancellationToken) =>
               {
                   var json = request.RequestUri!.AbsolutePath.Contains("/search/")
                       ? """{"results":[{"id":1,"title":"The Thing","release_date":"1982-06-25"},{"id":2,"title":"The Thing","release_date":"2011-10-14"}]}"""
                       : """{"results":{"US":{"flatrate":[{"provider_id":1,"provider_name":"Synthetic Streaming"}]}}}""";
                   return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
               });
        using var client = new HttpClient(handler.Object);
        var tmdb = new TmdbService(client, new ConfigurationBuilder().Build());
        var otherProvider = CreateProvider(new()
                                           {
                                               new()
                                               {
                                                   Id          = 1
                                                 , Title       = "The Thing"
                                                 , ReleaseDate = "1982-06-25"
                                                 , WebUrl      = "https://www.imdb.com/title/tt1/"
                                               }
                                           });
        var aggregator = new MovieDataAggregator(new IMovieDataProvider[] { tmdb, otherProvider.Object });

        var response = await aggregator.SearchMoviesAsync("The Thing");

        var results = Assert.IsType<List<MovieSearchResult>>(response.Data?.Results);
        Assert.Equal(new[] { 1, 2, 1 }, results.Select(result => result.Id));
        Assert.Equal(new[] { "1982-06-25", "2011-10-14", "1982-06-25" }, results.Select(result => result.ReleaseDate));
        Assert.All(results.Take(2), result => Assert.Equal(new[] { "Synthetic Streaming" }, result.StreamingProviders));
        Assert.Empty(results[2].StreamingProviders);
        Assert.Contains(nameof(TmdbService), response.Diagnostics.Keys);
    }

    [Fact]
    public async Task SearchMoviesAsync_SameTitleWithAmbiguousOrConflictingEvidence_KeepsRowsSeparate()
    {
        var rows = new List<MovieSearchResult>
                   {
                       new()
                       {
                           Id          = 1
                         , Title       = "Same"
                         , WebUrl      = "https://www.imdb.com/title/tt1/"
                         , ReleaseDate = "1982"
                       }
                     , new()
                       {
                           Id          = 1
                         , Title       = "Same"
                         , WebUrl      = "https://www.imdb.com/title/tt2/"
                         , ReleaseDate = "1982"
                       }
                     , new()
                       {
                           Id          = 1
                         , Title       = "Same"
                         , WebUrl      = "https://www.imdb.com/title/tt1/"
                         , ReleaseDate = "2011"
                       }
                     , new()
                       {
                           Id          = 1
                         , Title       = "Same"
                         , WebUrl      = "https://www.imdb.com/title/tt1/"
                         , ReleaseDate = "1982"
                         , MediaType   = "Show"
                       }
                     , new()
                       {
                           Id    = 1
                         , Title = "Same"
                       }
                     , new()
                       {
                           Id    = 1
                         , Title = "Same"
                       }
                   };
        var aggregator = new MovieDataAggregator(new[] { CreateProvider(rows).Object });

        var response = await aggregator.SearchMoviesAsync("Same");

        Assert.Equal(6, response.Data?.Results?.Count);
    }

    [Fact]
    public async Task SearchMoviesAsync_ConfirmedDuplicates_MergesMetadataWithoutChangingProviderObjects()
    {
        var first = new MovieSearchResult
                    {
                        Id                 = 1
                      , Title              = "Same"
                      , WebUrl             = "https://www.imdb.com/title/tt1/"
                      , StreamingProviders = new List<string> { "First" }
                      , AggregatedData     = new Dictionary<string, string> { ["Original"] = "Retained" }
                    };
        var second = new MovieSearchResult
                     {
                         Id                 = 1
                       , Title              = "Same"
                       , WebUrl             = first.WebUrl
                       , StreamingProviders = new List<string> { "Second" }
                     };
        var aggregator = new MovieDataAggregator(new[] { CreateProvider(new() { first, second }).Object });

        var response = await aggregator.SearchMoviesAsync("Same");
        var repeated = await aggregator.SearchMoviesAsync("Same");

        var merged = Assert.Single(response.Data!.Results!);
        Assert.Equal(new[] { "First", "Second" }, merged.StreamingProviders);
        Assert.Equal("Retained", merged.AggregatedData["Original"]);
        Assert.Single(repeated.Data!.Results!);
        Assert.Equal(new[] { "First" }, first.StreamingProviders);
        Assert.Empty(first.SourceApis);
        Assert.Single(first.AggregatedData);
        Assert.NotSame(first, merged);
    }

    [Fact]
    public async Task SearchMoviesAsync_ProviderFailureAndEmptyQuery_PreservesDiagnosticsAndSkipsDisabledProviders()
    {
        var failing = new Mock<IMovieDataProvider>();
        failing.SetupGet(provider => provider.IsEnabled).Returns(true);
        failing.Setup(provider => provider.SearchMoviesAsync("Same")).ThrowsAsync(new InvalidOperationException("Synthetic failure"));
        var disabled = new Mock<IMovieDataProvider>();
        var aggregator = new MovieDataAggregator(new[] { failing.Object, disabled.Object });

        var empty = await aggregator.SearchMoviesAsync(" ");
        var failure = await aggregator.SearchMoviesAsync("Same");

        Assert.Empty(empty.Data!.Results!);
        Assert.Empty(failure.Data!.Results!);
        Assert.Contains(failure.Diagnostics.Values, diagnostic => diagnostic.Contains("Synthetic failure"));
        failing.Verify(provider => provider.SearchMoviesAsync(It.IsAny<string>()), Times.Once);
        disabled.Verify(provider => provider.SearchMoviesAsync(It.IsAny<string>()), Times.Never);
    }

    private static Mock<IMovieDataProvider> CreateProvider(List<MovieSearchResult> rows)
    {
        var provider = new Mock<IMovieDataProvider>();
        provider.SetupGet(instance => instance.IsEnabled).Returns(true);
        provider.Setup(instance => instance.SearchMoviesAsync(It.IsAny<string>()))
                .ReturnsAsync(new AggregatedResult<MovieSearchResponse?>
                              {
                                  Data = new MovieSearchResponse { Results = rows }
                              });
        return provider;
    }
}
