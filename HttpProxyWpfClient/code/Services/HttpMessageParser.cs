using System.Collections;
using System.IO;
using System.Text;
using Titanium.Web.Proxy.Http;

namespace HttpProxyWpfClient.code.Services;

public sealed record ParsedHttpMessage(string StartLine, string HeadersText, string BodyText)
{
    public IReadOnlyList<(string Name, string Value)> Headers =>
        HeadersText.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line =>
            {
                int separator = line.IndexOf(':');
                return separator > 0
                    ? (Name: line[..separator].Trim(), Value: line[(separator + 1)..].Trim())
                    : (Name: string.Empty, Value: string.Empty);
            })
            .Where(header => header.Name.Length > 0)
            .ToArray();
}

public static class HttpMessageParser
{
    public static ParsedHttpMessage Parse(string? text)
    {
        text ??= string.Empty;
        int separator = FindSeparator(text, out int separatorLength);
        int lineEnd = FindLineEnd(text, out int lineLength);

        string startLine = text[..Math.Min(lineEnd, text.Length)];
        if (separator < 0)
        {
            string rest = lineEnd + lineLength < text.Length ? text[(lineEnd + lineLength)..] : string.Empty;
            return rest.Contains(':')
                ? new ParsedHttpMessage(startLine, rest, string.Empty)
                : new ParsedHttpMessage(startLine, string.Empty, rest);
        }

        int headersStart = Math.Min(lineEnd + lineLength, separator);
        string headers = text[headersStart..separator];
        int bodyStart = Math.Min(separator + separatorLength, text.Length);
        return new ParsedHttpMessage(startLine, headers, text[bodyStart..]);
    }

    public static void ApplyRequest(ParsedHttpMessage parsed, Request request)
    {
        string[] parts = parsed.StartLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0) request.Method = parts[0];
        if (parts.Length > 1)
        {
            if (Uri.TryCreate(parts[1], UriKind.Absolute, out Uri? absolute))
                request.RequestUri = absolute;
            else if (Uri.TryCreate(request.RequestUri, parts[1], out Uri? combined))
                request.RequestUri = combined;
        }

        ApplyHeaders(request.Headers, parsed.Headers);
    }

    public static void ApplyResponse(ParsedHttpMessage parsed, Response response)
    {
        string[] parts = parsed.StartLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && parts[0].StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase) &&
            Version.TryParse(parts[0][5..], out Version? version))
            response.HttpVersion = version;
        if (parts.Length > 1 && int.TryParse(parts[1], out int statusCode))
            response.StatusCode = statusCode;
        if (parts.Length > 2) response.StatusDescription = parts[2];

        ApplyHeaders(response.Headers, parsed.Headers);
    }

    public static byte[] GetBodyBytes(ParsedHttpMessage parsed, Response response)
    {
        string? transferEncoding = GetHeaderValue(response.Headers, "Transfer-Encoding");
        if (transferEncoding?.Contains("chunked", StringComparison.OrdinalIgnoreCase) == true)
        {
            try { return DecodeChunkedBody(parsed.BodyText); }
            catch { }
        }

        return Encoding.UTF8.GetBytes(parsed.BodyText);
    }

    public static string? GetHeaderValue(object? headers, string name)
    {
        if (headers == null) return null;
        try
        {
            var type = headers.GetType();
            var indexer = type.GetProperty("Item");
            if (indexer?.GetValue(headers, new object[] { name }) is object value)
                return value.ToString();

            var method = type.GetMethod("Get") ?? type.GetMethod("GetHeader") ?? type.GetMethod("GetValues");
            if (method?.Invoke(headers, new object[] { name }) is IEnumerable<string> values)
                return string.Join(", ", values);
            return method?.Invoke(headers, new object[] { name })?.ToString();
        }
        catch { return null; }
    }

    public static byte[] DecodeChunkedBody(string chunked)
    {
        if (string.IsNullOrEmpty(chunked)) return Array.Empty<byte>();
        using var output = new MemoryStream();
        int position = 0;
        while (position < chunked.Length)
        {
            int lineEnd = chunked.IndexOf("\r\n", position, StringComparison.Ordinal);
            int newlineLength = 2;
            if (lineEnd < 0)
            {
                lineEnd = chunked.IndexOf('\n', position);
                newlineLength = 1;
            }
            if (lineEnd < 0) break;

            string sizeText = chunked[position..lineEnd].Trim();
            position = lineEnd + newlineLength;
            if (sizeText.Length == 0) continue;
            int size = Convert.ToInt32(sizeText.Split(';')[0], 16);
            if (size == 0) break;
            if (position + size > chunked.Length) throw new FormatException("Chunk exceeds body length.");
            byte[] bytes = Encoding.UTF8.GetBytes(chunked.Substring(position, size));
            output.Write(bytes, 0, bytes.Length);
            position += size;
            if (chunked.AsSpan(position).StartsWith("\r\n")) position += 2;
            else if (position < chunked.Length && chunked[position] == '\n') position++;
        }
        return output.ToArray();
    }

    private static void ApplyHeaders(object headers, IReadOnlyList<(string Name, string Value)> parsedHeaders)
    {
        try { headers.GetType().GetMethod("Clear")?.Invoke(headers, null); } catch { }
        foreach (var (name, value) in parsedHeaders)
        {
            try { headers.GetType().GetMethod("AddHeader")?.Invoke(headers, new object[] { name, value }); }
            catch { }
        }
    }

    private static int FindSeparator(string text, out int length)
    {
        foreach (var separator in new[] { "\r\n\r\n", "\n\n", "\r\r" })
        {
            int index = text.IndexOf(separator, StringComparison.Ordinal);
            if (index >= 0) { length = separator.Length; return index; }
        }
        length = 0;
        return -1;
    }

    private static int FindLineEnd(string text, out int length)
    {
        int index = text.IndexOf("\r\n", StringComparison.Ordinal);
        if (index >= 0) { length = 2; return index; }
        index = text.IndexOf('\n');
        if (index >= 0) { length = 1; return index; }
        length = 0;
        return text.Length;
    }
}
