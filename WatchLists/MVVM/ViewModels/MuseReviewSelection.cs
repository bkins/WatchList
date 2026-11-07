using WatchLists.MVVM.Models;
using WatchLists.Services.MuseImport;

namespace WatchLists.MVVM.ViewModels;

internal sealed record MuseReviewSelection(WatchItem? Existing
                                         , MuseProviderCandidate? Provider
                                         , string? Category);
