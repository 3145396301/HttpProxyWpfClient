## v1.0.5

### 新功能
- 会话列表右键新增「预览响应图片」：图片类响应直接弹窗查看，body 在代理 BeforeResponse 阶段已解压缓存，无需重新请求。
- 会话列表右键新增「替换响应图片」：处于响应阶段拦截中的会话，可用本地图片替换响应体；替换后不自动放行，仍由用户点击「放行」释放拦截。
- 「编辑完整请求体/响应体」弹窗支持 Ctrl+F 搜索定位：查看超大报文时可在完整内容中搜索并高亮跳转。
- 补齐图片预览/替换等功能的中文 / English / 日本語 / 한국어 多语言资源。

### 依赖与工程
- 升级 Titanium.Web.Proxy 至 7.0.4（原 6.0.2）。
- .NET SDK 固定为 10.0.400（`rollForward: latestFeature`）。
- CI 修复：构建改为针对 .csproj，避免 NETSDK1194 导致框架依赖版与自包含版产物互相覆盖。
- CI 每次构建同时产出框架依赖版与自包含版（内置 .NET 10 运行时）两个 zip Artifact。
- 项目更名为 HttpProxyWpfClient，添加 GPL-3.0 开源协议证书。

### 重构
- Main 页面按职责拆分为 Configuration / Editor / Lifecycle / Proxy / SessionLifecycle 分部类，移除旧版事件处理器与冗余代码，无功能变化。
