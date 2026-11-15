namespace WatchLists.MVVM.Models;

public sealed record UnverifiedMuseItem(string Title
                                      , int? ReleaseYear
                                      , string Description
                                      , string Section
                                      , string SourceUrl
                                      , int SourceLineNumber);
