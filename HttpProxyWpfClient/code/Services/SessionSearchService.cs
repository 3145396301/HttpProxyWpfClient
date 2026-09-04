using HttpProxyWpfClient.code.net.entity;

namespace HttpProxyWpfClient.code.Services;

public static class SessionSearchService
{
    public static IEnumerable<SearchResultItem> Search(IEnumerable<RequestVo> sessions, SearchOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Keyword) || options.Fields.Count == 0)
            yield break;

        foreach (RequestVo session in sessions)
        {
            foreach (SearchField field in options.Fields)
            {
                string text = session.GetSearchableText(field);
                foreach (var (start, length) in SearchEngine.FindMatches(text, options))
                {
                    yield return new SearchResultItem(
                        session,
                        field,
                        text.Substring(start, length),
                        start,
                        SearchEngine.BuildSnippet(text, start, length));
                }
            }
        }
    }
}
