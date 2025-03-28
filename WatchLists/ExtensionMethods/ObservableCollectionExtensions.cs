﻿using System.Collections.ObjectModel;
using System.Diagnostics;

namespace WatchLists.ExtensionMethods;

public static class ObservableCollectionExtensions
{
    public static bool DoesNotContains<T> (this IEnumerable<T> source
                                     , T                   searchItem)
    {
        return ! source.Contains(searchItem);
    }
}
