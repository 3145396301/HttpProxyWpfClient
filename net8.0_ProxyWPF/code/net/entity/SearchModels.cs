using System.Text.RegularExpressions;

namespace net8._0_ProxyWPF.code.net.entity
{
    /// <summary>
    /// 搜索范围字段（可多选）
    /// </summary>
    public enum SearchField
    {
        Host,
        Url,
        Method,
        StatusCode,
        RequestHeaders,
        RequestBody,
        ResponseHeaders,
        ResponseBody
    }

    /// <summary>
    /// 单条搜索结果：命中的会话 + 命中字段 + 命中的实际文本。
    /// 不存储原始偏移量：跳转高亮时会在实际渲染的编辑器文本中重新定位该文本，避免不同来源之间的偏移量换算误差。
    /// </summary>
    public class SearchResultItem
    {
        public RequestVo RequestVo { get; }
        public SearchField Field { get; }

        /// <summary>
        /// 命中的实际文本内容，用于跳转时在渲染文本中重新定位
        /// </summary>
        public string MatchedText { get; }

        /// <summary>
        /// 命中处的上下文摘要（截取命中位置前后一段文本），用于侧边栏结果列表展示
        /// </summary>
        public string Snippet { get; }

        public string FieldDisplayName => Field switch
        {
            SearchField.Host => "域名",
            SearchField.Url => "URL",
            SearchField.Method => "方法",
            SearchField.StatusCode => "状态码",
            SearchField.RequestHeaders => "请求头",
            SearchField.RequestBody => "请求体",
            SearchField.ResponseHeaders => "响应头",
            SearchField.ResponseBody => "响应体",
            _ => Field.ToString()
        };

        /// <summary>
        /// 该字段属于请求侧还是响应侧，决定跳转时定位到哪个编辑器
        /// </summary>
        public bool IsRequestSide => Field is SearchField.Host or SearchField.Url or SearchField.Method
            or SearchField.RequestHeaders or SearchField.RequestBody;

        public SearchResultItem(RequestVo requestVo, SearchField field, string matchedText, string snippet)
        {
            RequestVo = requestVo;
            Field = field;
            MatchedText = matchedText;
            Snippet = snippet;
        }
    }

    /// <summary>
    /// 搜索条件：关键字、是否正则、启用的搜索范围字段
    /// </summary>
    public class SearchOptions
    {
        public string Keyword { get; set; } = "";
        public bool UseRegex { get; set; }
        public HashSet<SearchField> Fields { get; set; } = new HashSet<SearchField>
        {
            SearchField.Host, SearchField.Url, SearchField.Method, SearchField.StatusCode,
            SearchField.RequestHeaders, SearchField.RequestBody,
            SearchField.ResponseHeaders, SearchField.ResponseBody
        };
    }

    /// <summary>
    /// 在给定文本上执行"包含"或"正则"匹配，返回所有命中位置
    /// </summary>
    public static class SearchEngine
    {
        /// <summary>
        /// 在单个字段文本中查找所有命中位置（大小写不敏感的包含匹配，或用户指定的正则表达式）。
        /// 正则非法时返回空结果，不抛出异常，避免影响其余字段的搜索。
        /// </summary>
        public static IEnumerable<(int Start, int Length)> FindMatches(string text, SearchOptions options)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(options.Keyword))
            {
                yield break;
            }

            if (options.UseRegex)
            {
                Regex regex;
                try
                {
                    regex = new Regex(options.Keyword, RegexOptions.IgnoreCase);
                }
                catch (ArgumentException)
                {
                    yield break;
                }

                Match match = regex.Match(text);
                while (match.Success)
                {
                    yield return (match.Index, match.Length);
                    if (match.Length == 0)
                    {
                        // 避免零宽匹配导致死循环
                        if (match.Index + 1 > text.Length) break;
                        match = regex.Match(text, match.Index + 1);
                    }
                    else
                    {
                        match = match.NextMatch();
                    }
                }
            }
            else
            {
                int index = 0;
                while (true)
                {
                    int found = text.IndexOf(options.Keyword, index, StringComparison.OrdinalIgnoreCase);
                    if (found < 0) break;
                    yield return (found, options.Keyword.Length);
                    index = found + Math.Max(options.Keyword.Length, 1);
                    if (index > text.Length) break;
                }
            }
        }

        /// <summary>
        /// 截取命中位置附近的上下文，用于结果列表展示（单行、限定长度，避免超长文本撑爆侧边栏）
        /// </summary>
        public static string BuildSnippet(string text, int matchStart, int matchLength, int contextLength = 40)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            int start = Math.Max(0, matchStart - contextLength);
            int end = Math.Min(text.Length, matchStart + matchLength + contextLength);
            string snippet = text.Substring(start, end - start);
            snippet = snippet.Replace("\r", " ").Replace("\n", " ");

            if (start > 0) snippet = "…" + snippet;
            if (end < text.Length) snippet += "…";
            return snippet;
        }
    }
}
