using Xunit;

namespace WatchList.Import.Tests;

internal sealed class FirebaseEmulatorFactAttribute : FactAttribute
{
    public FirebaseEmulatorFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("WATCHLIST_ENH12_EMULATOR_TEST") != "1")
            Skip = "Run against the isolated Firebase demo project by enabling the emulator tests.";
    }
}
