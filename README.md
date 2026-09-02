# net8.0_ProxyWPF

一个基于 [Titanium.Web.Proxy](https://github.com/titanium007/Titanium.Web.Proxy) 的 Windows 桌面 HTTP/HTTPS 抓包与调试代理工具，界面采用 WPF-UI 的 Fluent 深色主题。

它可以作为系统代理运行，对 HTTP/HTTPS 流量进行中间人解密，实时查看每个请求/响应的完整报文；支持按域名、URL、方法、请求头配置拦截规则，在请求或响应阶段「断点」拦截并在线修改报文后放行，非常适合调试接口、定位前后端问题。

## 功能特性

- **本地 HTTP/HTTPS 代理**：默认监听 `127.0.0.1:8000`，可一键设置为系统代理，退出程序时自动还原系统代理设置。
- **HTTPS 中间人解密**：自动生成并信任根证书，解密 HTTPS 流量以查看明文报文。
- **上游代理转发**：支持将流量转发到上游 HTTP/SOCKS 代理（可配置认证信息），可单独启用/禁用。
- **实时抓包列表**：展示 上行/下行拦截状态指示、Host、协议、方法、路径、进程信息；选中会话即可在右侧查看完整的请求/响应原文。
- **拦截规则（分组管理）**：
  - 按分组组织多条规则，分组与规则均可独立启用/禁用；
  - 匹配维度：域名、URL、请求方法、请求头（key/value），每个字段均支持正则表达式；
  - 支持「匹配所有请求」。
- **请求/响应断点拦截**：
  - 上行拦截：请求转发前暂停，可修改请求后放行；
  - 下行拦截：响应返回前暂停，可修改响应后放行；
  - 「全部放行」一键释放所有被拦截的会话，避免死锁。
- **在线编辑报文**：使用 AvalonEdit 编辑器直接修改请求/响应首行、Header 与 Body；编辑完成后点击「放行」将修改解析回真实报文并继续转发。
- **大内容截断保护**：超过 20 万字符的报文自动截断显示（切换为只读），通过「编辑完整请求体/响应体」弹窗查看和修改完整内容，避免超大文本渲染导致 UI 卡顿。
- **全文搜索**：`Ctrl+F` 打开搜索侧边栏，跨 域名 / URL / 方法 / 状态码 / 请求头 / 请求体 / 响应头 / 响应体 搜索，支持正则表达式，双击结果可跳转并在编辑器中高亮定位。
- **列表过滤**：「只展示拦截请求」仅显示命中拦截规则的会话；「直接丢弃非拦截请求」让非拦截流量仅转发不渲染，降低内存与界面开销。
- **复制为 curl**：会话右键一键复制请求为 `curl` 命令，支持 cmd / bash / PowerShell 三种 shell 语法。
- **错误提示**：请求失败或未完成时，响应区域展示错误信息，点击可查看完整错误详情。
- **响应体编码识别**：按响应头 `charset` 解码响应体，未声明字符集时按 UTF-8 解码，避免中文乱码。
- **配置持久化**：本地/上游代理设置与拦截规则保存到程序目录下的 `config.json`，下次启动自动加载。
- **多语言资源**：内置 中文 / English / 日本語 / 한국어 语言资源文件。

## 技术栈

- **.NET**：.NET 10（`net10.0-windows`），WPF
- **代理核心**：[Titanium.Web.Proxy](https://github.com/titanium007/Titanium.Web.Proxy) 6.0.2
- **界面**：[WPF-UI](https://github.com/lepoco/wpfui) 4.3.0（Fluent 设计，深色主题）
- **文本编辑**：[AvalonEdit](https://github.com/icsharpcode/AvalonEdit) 6.3.1

## 快速开始

### 环境要求

- Windows 10/11
- **运行程序**（选择其一）：
  - 推荐下载 **自包含版**（文件名含 `SelfContained`）：已内置 .NET 10 运行时，无需额外安装，解压即可运行；
  - 或下载 **框架依赖版**：需要先安装 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)。
- **从源码构建**：需要 [.NET 10 SDK](https://dotnet.microsoft.com/download)（项目在 `global.json` 中声明 `10.0.0`，`rollForward: latestFeature`）

### 构建与运行

```bash
dotnet build net8.0_ProxyWPF.sln
dotnet run --project net8.0_ProxyWPF
```

或使用 Visual Studio / Rider 打开 `net8.0_ProxyWPF.sln` 直接运行。

### 使用步骤

1. 启动程序后，代理会自动启动并设置为系统代理（默认监听 `127.0.0.1:8000`）。
2. 首次抓取 HTTPS 流量时按提示信任代理根证书（Titanium.Web.Proxy 自动签发）。
3. 浏览任意页面或发起接口请求，左侧会话列表会实时显示抓到的请求。
4. 点击会话查看完整请求/响应报文。
5. 若需要配置或修改拦截规则，点击顶部的「拦截」按钮打开规则管理弹窗。
6. 需要停止代理时，进入 **Setting** 页面点击「关闭代理」；关闭程序时也会自动还原系统代理设置。

## 拦截规则说明

拦截规则以「分组」为单位管理：

- 分组内包含多条规则，分组或单条规则未启用时不参与匹配。
- 一条规则可同时配置多个匹配条件，命中规则需要满足所有已填条件（域名、URL、方法、请求头）。
- 每条规则可独立选择：
  - **上行拦截**（请求阶段断点）；
  - **下行拦截**（响应阶段断点）。
- 匹配字段默认执行忽略大小写的「包含」匹配，勾选正则开关后按正则表达式匹配。

示例：拦截 `api.example.com` 下所有 POST 请求的响应并在线修改返回内容。

## 配置说明

配置文件为程序运行目录下的 `config.json`，字段如下：

```jsonc
{
  "LocalProxyHost": "0.0.0.0",   // 本地代理监听地址
  "LocalProxyPort": 8000,        // 本地代理监听端口
  "UpstreamEnabled": true,       // 是否启用上游代理
  "UpstreamHost": "127.0.0.1",   // 上游代理地址
  "UpstreamPort": 10808,         // 上游代理端口
  "UpstreamUser": null,          // 上游代理用户名（可选）
  "UpstreamPass": null,          // 上游代理密码（可选）
  "Groups": []                   // 拦截规则分组
}
```

配置在程序退出或保存拦截规则时写入；文件不存在或解析失败时会回退到默认配置。

## 项目结构

```
net8.0_ProxyWPF/
├── App.xaml(.cs)              # 应用入口（加载深色主题、多语言资源）
├── MainWindow.xaml(.cs)       # 主窗口（标题栏、导航、页面框架）
├── code/
│   ├── Pages/
│   │   ├── Main.xaml(.cs)     # 抓包主页面（会话列表、报文详情、搜索）
│   │   ├── Setting/           # 代理设置页
│   │   ├── BlockingSetting/   # 拦截规则管理弹窗
│   │   └── Util/              # 弹窗帮助、搜索高亮渲染
│   ├── net/
│   │   ├── ProxyConnect.cs    # 代理服务器封装（启动/停止/系统代理/改包）
│   │   ├── ConfigService.cs   # config.json 读写
│   │   ├── entity/            # 会话、规则、搜索等实体与视图模型
│   │   └── util/              # curl 命令生成等工具
│   ├── Task/TaskChain.cs      # 请求/响应处理的任务链（按优先级执行）
│   ├── Loc/                   # 多语言资源加载
│   └── base/BindableBase.cs   # MVVM 绑定基类
└── Resources/                 # 多语言字符串与图标资源
```

### 核心处理流程

`ProxyConnect` 将 Titanium.Web.Proxy 的请求/响应事件组织成三个可插拔的「任务链」（`TaskChain`，按优先级依次执行）：

- `BeforeRequest`：读取请求体 → URL 打印（加入会话列表）→ 请求拦截（命中则断点等待放行）；
- `BeforeResponse`：刷新详情界面 → 响应拦截（命中则断点等待放行）；
- `AfterResponse`：再次刷新界面、清理暂存会话。

拦截通过 `Monitor.Wait/Pulse` 阻塞代理线程实现，放行时把编辑后的报文解析回真实 `Request`/`Response` 后 `Pulse` 唤醒。

## 发布

项目内置 GitHub Actions 工作流（`.github/workflows/dotnet-desktop.yml`）：

- 推送 `main` 分支或 PR 时自动构建 `Release`；
- 每次构建同时产出两种 zip，并分别上传为独立 Artifact：
  - `ProxyWPF-net10.0-Release-FrameworkDependent.zip`：框架依赖版，不含运行时，体积约为数 MB，用户需自行安装 .NET 10 Desktop Runtime；
  - `ProxyWPF-net10.0-Release-SelfContained-win-x64.zip`：自包含版，内置 win-x64 的 .NET 10 Desktop Runtime，体积明显更大，解压即用；
- 推送 `v*` 标签时自动创建 GitHub Release，同时附上两种 zip，说明取自 `CHANGELOG.md`。

## 更新日志

各版本变更见 [CHANGELOG.md](CHANGELOG.md)。

## 协议

本项目基于 [GNU General Public License v3.0](LICENSE)（GPL-3.0）发布。

这意味着你可以自由使用、修改和分发本程序（含修改后的衍生版本），但任何分发都必须在相同的 GPL-3.0 条款下进行，并提供对应的源代码。
