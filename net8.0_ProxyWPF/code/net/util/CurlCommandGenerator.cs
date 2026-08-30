using System.Text;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;

namespace net8._0_ProxyWPF.code.net.util
{
    public enum CurlShellType
    {
        Cmd,
        Bash,
        PowerShell
    }

    public static class CurlCommandGenerator
    {
        public static string Generate(Request request, CurlShellType shellType)
        {
            string curlExe = shellType == CurlShellType.PowerShell ? "curl.exe" : "curl";
            List<string> parts = new List<string> { curlExe };

            parts.Add("-X");
            parts.Add(Quote(request.Method, shellType));
            parts.Add(Quote(request.RequestUri.ToString(), shellType));

            foreach (HttpHeader header in request.Headers)
            {
                if (string.Equals(header.Name, "Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    // curl 会根据实际数据自动重新计算，携带旧值可能导致请求体不匹配
                    continue;
                }
                parts.Add("-H");
                parts.Add(Quote($"{header.Name}: {header.Value}", shellType));
            }

            if (request.HasBody)
            {
                string body = Encoding.UTF8.GetString(request.Body);
                parts.Add("--data-raw");
                parts.Add(Quote(body, shellType));
            }

            return JoinWithLineContinuation(parts, shellType);
        }

        private static string JoinWithLineContinuation(List<string> parts, CurlShellType shellType)
        {
            string continuation = shellType switch
            {
                CurlShellType.Cmd => " ^\r\n  ",
                CurlShellType.Bash => " \\\n  ",
                CurlShellType.PowerShell => " `\r\n  ",
                _ => " "
            };

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0 && (parts[i - 1] == "-X" || parts[i - 1] == "-H" || parts[i - 1] == "--data-raw"))
                {
                    sb.Append(' ').Append(parts[i]);
                }
                else if (i > 0)
                {
                    sb.Append(continuation).Append(parts[i]);
                }
                else
                {
                    sb.Append(parts[i]);
                }
            }
            return sb.ToString();
        }

        private static string Quote(string value, CurlShellType shellType)
        {
            switch (shellType)
            {
                case CurlShellType.Bash:
                    // 单引号包裹，内部单引号用 '"'"' 转义
                    return "'" + value.Replace("'", "'\"'\"'") + "'";
                case CurlShellType.PowerShell:
                    // 双引号包裹，反引号转义 $、`、"
                    return "\"" + value.Replace("`", "``").Replace("$", "`$").Replace("\"", "`\"") + "\"";
                case CurlShellType.Cmd:
                default:
                    // 双引号包裹，内部双引号翻倍转义；换行替换为 ^ 续行
                    string escaped = value.Replace("\"", "\"\"");
                    escaped = escaped.Replace("\r\n", "^\r\n").Replace("\n", "^\n");
                    return "\"" + escaped + "\"";
            }
        }
    }
}
