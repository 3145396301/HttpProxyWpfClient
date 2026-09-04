using System;
using System.Text;
using System.Text.RegularExpressions;
using HttpProxyWpfClient.code.@base;
using HttpProxyWpfClient.code.Loc;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;

namespace HttpProxyWpfClient.code.net.entity
{
    public class ResponseMessage : BindableBase
    {
        /// <summary>
        /// 用于界面展示的最大字符数，超出后截断显示，避免 WPF TextBox 渲染超大文本导致 UI 卡顿
        /// </summary>
        public const int MaxDisplayLength = 200_000;

        private string _respRow;
        private string _respHeaders;
        private string _respBody;
        private Exception _error;
        public string RespRow
        {
            get => _respRow;
            set => SetProperty(ref _respRow, value);
        }
        public string RespHeaders
        {
            get => _respHeaders;
            set => SetProperty(ref _respHeaders, value);
        }

        public string RespBody
        {
            get => _respBody;
            set
            {
                SetProperty(ref _respBody, value);
                RecomputeIsTruncated();
                OnPropertyChanged(nameof(AllMessage));
                OnPropertyChanged(nameof(DisplayMessage));
            }
        }

        public Exception Error
        {
            get => _error;
            set
            {
                SetProperty(ref _error, value);
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(ErrorText));
            }
        }

        public bool HasError => Error != null;

        public string ErrorText => Error != null
            ? string.Format(LocalizationManager.GetString("RequestIncompletePrefix"), Error.Message)
            : "";

        /// <summary>
        /// 完整的响应原文（首行+body），用于放行时解析，不用于界面展示
        /// </summary>
        public string AllMessage
        {
            get
            {
                return $"{RespRow}{RespBody??""}";
            }
            set
            {

            }
        }

        private bool _isTruncated;

        /// <summary>
        /// 是否因内容过大而被截断展示
        /// </summary>
        public bool IsTruncated
        {
            get => _isTruncated;
            private set => SetProperty(ref _isTruncated, value);
        }

        /// <summary>
        /// 界面展示用文本：内容过大时截断，避免 TextBox 一次性渲染超大文本导致卡顿。
        /// 纯读取、不产生副作用，IsTruncated 由 RecomputeIsTruncated 主动维护，避免绑定求值顺序不确定导致 IsTruncated 滞后。
        /// </summary>
        public string DisplayMessage
        {
            get
            {
                string full = AllMessage;
                if (full.Length <= MaxDisplayLength)
                {
                    return full;
                }

                return full.Substring(0, MaxDisplayLength)
                       + "\r\n\r\n"
                       + string.Format(LocalizationManager.GetString("ContentTruncated"),
                           MaxDisplayLength.ToString("N0"), full.Length.ToString("N0"),
                           LocalizationManager.GetString("EditFullResponseBody"));
            }
        }

        private void RecomputeIsTruncated()
        {
            IsTruncated = AllMessage.Length > MaxDisplayLength;
        }

        public ResponseMessage(SessionEventArgs session):this(session.HttpClient.Response)
        {
            if (session.Exception != null)
            {
                Error = session.Exception;
            }
        }

        public ResponseMessage(Response resp)
        {
            RespRow = resp.HeaderText;
            if (resp.HasBody)
            {
                try
                {
                    RespBody = DecodeResponseBody(resp);
                }
                catch (Exception e)
                {
                    Error = new Exception(LocalizationManager.GetString("ResponseBodyNotReady"), e);
                }

            }
            else
            {
                RecomputeIsTruncated();
            }

            if (Error == null && resp.StatusCode == 0)
            {
                Error = new Exception(LocalizationManager.GetString("NoValidResponse"));
            }

        }

        /// <summary>
        /// 将响应体字节按响应头声明的字符集解码为文本。
        /// HTTP 规范在未声明 charset 时默认按 ISO-8859-1，但实际 JSON/文本接口几乎都是 UTF-8；
        /// 这里在未声明 charset 或声明为 utf-8 时使用 UTF-8，避免中文出现乱码。
        /// </summary>
        public static string DecodeResponseBody(Response resp)
        {
            if (resp == null || !resp.HasBody || resp.Body == null)
            {
                return "";
            }

            string? charset = null;
            try
            {
                var contentType = resp.Headers?.FirstOrDefault(
                    h => string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))?.Value;
                if (!string.IsNullOrEmpty(contentType))
                {
                    var match = Regex.Match(contentType, @"charset\s*=\s*[""']?([^;""']+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        charset = match.Groups[1].Value.Trim().Trim('"', '\'');
                    }
                }
            }
            catch
            {
                // 读取头失败时回退 UTF-8
            }

            if (string.IsNullOrEmpty(charset)
                || string.Equals(charset, "utf-8", StringComparison.OrdinalIgnoreCase)
                || string.Equals(charset, "utf8", StringComparison.OrdinalIgnoreCase))
            {
                return Encoding.UTF8.GetString(resp.Body);
            }

            try
            {
                return Encoding.GetEncoding(charset).GetString(resp.Body);
            }
            catch
            {
                // 当前运行时若不支持该编码，回退 UTF-8，避免整个响应解析失败
                return Encoding.UTF8.GetString(resp.Body);
            }
        }
    }
}
