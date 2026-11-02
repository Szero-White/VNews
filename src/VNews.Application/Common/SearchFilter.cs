namespace VNews.Application.Common;

/// <summary>How far back an article may have been published.</summary>
public enum SearchPeriod { All, Day, Week, Month, Year }

/// <summary>Relevance puts a whole-phrase title hit first; Latest is purely newest first.</summary>
public enum SearchSort { Relevance, Latest }

/// <summary>Articles written in the newsroom versus headlines imported from another publisher's feed.</summary>
public enum SearchKind { All, Original, External }

/// <summary>The narrowing options of the search page. The default (no filter, relevance) is what the API and older callers get.</summary>
public sealed record SearchFilter(
    string? CategorySlug = null,
    SearchPeriod Period = SearchPeriod.All,
    SearchKind Kind = SearchKind.All,
    SearchSort Sort = SearchSort.Relevance);
