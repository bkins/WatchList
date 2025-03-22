using WatchLists.Services;
using WatchLists.Services.Models;

namespace WatchLists.Utilities;

public class ApiUtility
{
    public static bool TryParseId (string  input
                                 , out int id) =>
            int.TryParse(input
                       , out id);

    public static AggregatedResult<T> CreateAggregatedError<T> (string errorMessage)
    {
        var result = new AggregatedResult<T>
                     {
                             Data = default
                     };
        result.Diagnostics.Add("ApiUtility"
                             , errorMessage);

        return result;
    }

    public static async Task<AggregatedResult<T>> TryParseAndExecuteAsync<T> (
            string                               input
          , Func<int, Task<AggregatedResult<T>>> apiMethod
          , string                               errorContext)
    {
        if (! TryParseId(input
                       , out int id))
        {
            return CreateAggregatedError<T>($"Invalid {errorContext}.");
        }

        return await apiMethod(id);
    }

    public static async Task<AggregatedResult<T>> TryParseAndExecuteDiagnosticsAsync<T> (
            string                               input
          , Func<int, Task<AggregatedResult<T>>> apiMethod
          , string                               errorContext
          , MovieDataAggregator                  aggregator)
    {
        if (! TryParseId(input
                       , out int id))
        {
            return CreateAggregatedError<T>($"Invalid {errorContext}.");
        }

        // Here, we assume aggregator has a generic ExecuteWithDiagnosticsAsync method.
        return await aggregator.ExecuteWithDiagnosticsAsync(provider => apiMethod(id));
    }
}
