using System.ComponentModel;
using System.Text;
using net8._0_ProxyWPF.code.@base;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;

namespace net8._0_ProxyWPF.code.net.entity
{
    public class RequestMessage  : BindableBase
    {
        /// <summary>
        /// 用于界面展示的最大字符数，超出后截断显示，避免 WPF TextBox 渲染超大文本导致 UI 卡顿
        /// </summary>
        public const int MaxDisplayLength = 200_000;

        private string _reqRow;
        private string _reqHeaders;
        private string _reqBody;

        public string ReqRow
        {
            get => _reqRow;
            set => SetProperty(ref _reqRow, value);
        }
        public string ReqHeaders
        {
            get => _reqHeaders;
            set => SetProperty(ref _reqHeaders, value);
        }
        public string ReqBody
        {
            get => _reqBody;
            set
            {
                SetProperty(ref _reqBody, value);
                OnPropertyChanged(nameof(AllMessage));
                OnPropertyChanged(nameof(DisplayMessage));
            }
        }

        /// <summary>
        /// 完整的请求原文（首行+body），用于放行时解析，不用于界面展示
        /// </summary>
        public string AllMessage
        {
            get{ return $"{ReqRow}{ReqBody??""}"; }
            set
            {

            }
        }

        /// <summary>
        /// 是否因内容过大而被截断展示
        /// </summary>
        public bool IsTruncated { get; private set; }

        /// <summary>
        /// 界面展示用文本：内容过大时截断，避免 TextBox 一次性渲染超大文本导致卡顿
        /// </summary>
        public string DisplayMessage
        {
            get
            {
                string full = AllMessage;
                if (full.Length <= MaxDisplayLength)
                {
                    IsTruncated = false;
                    return full;
                }

                IsTruncated = true;
                return full.Substring(0, MaxDisplayLength)
                       + $"\r\n\r\n[内容过大，已截断显示（{MaxDisplayLength:N0}/{full.Length:N0} 字符），编辑框已切换为只读，请使用下方“编辑完整请求体”按钮修改]";
            }
        }

        public RequestMessage(SessionEventArgs session):this(session.HttpClient.Request)
        {
        }

        public  RequestMessage (Request req)
        {
            ReqRow = req.HeaderText;
            HeaderCollection headerCollection = req.Headers;
            foreach (HttpHeader httpHeader in headerCollection)
            {
                ReqHeaders += $"{httpHeader.Name}:{httpHeader.Value}\r\n";
            }
            if (!req.HasBody)
            {
                ReqBody = "";
                return;
            }
            ReqBody = Encoding.UTF8.GetString(req.Body);
        }


    }
}