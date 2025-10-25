# 微信小程序 + ASP.NET Core WebSocket 骨架

## 项目简介
本仓库提供一个最小可运行的微信小程序客户端与 ASP.NET Core WebSocket 后端示例，用于演示“输入邮箱后通过 WebSocket 请求验证码”的完整流程。后端仅返回模拟结果，方便后续扩展真实的验证码发送逻辑。

## 目录结构
```
client/
  miniprogram/
    app.js
    app.json
    app.wxss
    config.js
    project.config.json
    utils/
      ws.js
    pages/
      index/
        index.js
        index.json
        index.wxml
        index.wxss
server/
  aspnetcore-ws/
    Program.cs
    aspnetcore-ws.csproj
src/
  MultiTabExplorer/        # 历史示例项目（与本次骨架无直接关联）
```

## 本地开发流程
### 1. 启动后端（ASP.NET Core WebSocket）
1. 安装 [.NET SDK 8](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0)。
2. 打开终端并执行：
   ```bash
   cd server/aspnetcore-ws
   dotnet restore
   dotnet run
   ```
3. 开发环境默认监听地址为 `http://localhost:6000`，WebSocket 端点为 `ws://localhost:6000/ws`。终端会输出收到的请求与返回的结果。

> 提示：仓库已提供 `Properties/launchSettings.json`，`dotnet run` 会自动使用 `ASPNETCORE_ENVIRONMENT=Development` 并监听 6000 端口。

### 2. 运行微信小程序客户端
1. 安装 [微信开发者工具](https://developers.weixin.qq.com/miniprogram/dev/devtools/download.html)。
2. 打开微信开发者工具，选择“导入项目”，项目目录指向 `client/miniprogram`。
3. 使用测试号时可保持 `project.config.json` 内的 `appid` 为 `touristappid`，实际项目请替换为有效 AppID。
4. 在“详情”设置中关闭“校验合法域名、web-view（业务域名）、TLS 版本以及 HTTPS 证书”，以便本地调试 WebSocket。
5. 编译并预览小程序：
   - 输入有效邮箱后按钮会变为可点击。
   - 点击“获取验证码”即向后端发送 JSON 请求。
   - 收到响应后会弹出 Toast 并在页面下方展示状态。

### 3. 页面交互与调试提示
- 页面包含邮箱输入框与“获取验证码”按钮，顶部状态条会显示“连接中 / 已连接 / 未连接 / 重试次数”，断线后会自动重试直至达到设定的最大次数。
- 邮箱输入框采用文本类型配合正则校验，输入非法邮箱时按钮会禁用并提示“邮箱格式不正确”。
- 点击按钮后会通过 WebSocket 发送 `{ type: "RequestCode", email }`，收到 `{ type: "CodeSent", success, message }` 后以 Toast 呈现服务端返回内容。
- 微信开发者工具中可通过“调试器 → Network → WS”查看建连、断开与消息收发详情，本地调试时请确保已关闭合法域名校验。
- 体验版 / 正式版需在“小程序后台 → 开发设置”中配置 `wss://kccoding.top` 为 socket 合法域名，并保持默认的 `/wechat/ws` 接口路径。

`client/miniprogram/config.js` 会根据小程序当前环境自动选择 WS 地址：
- 开发版（develop）：`ws://localhost:6000/ws`
- 体验版 / 线上版（trial/release）：`wss://kccoding.top/wechat/ws`

## 生产部署指引
### 1. Kestrel 监听端口与协议
- 生产环境通过 Kestrel 直接监听 `https://0.0.0.0:443`，提供 `/wechat/ws` 与回退兼容的 `/ws` 两个 WebSocket 路径。
- 同时监听 `http://0.0.0.0:80` 并自动重定向至 HTTPS（`app.UseHttpsRedirection()`）。
- 若仅在本地开发，继续使用 `http://0.0.0.0:6000`。

### 2. 证书加载（.pfx）
- 通过环境变量提供证书路径与密码：
  - `CERT_PATH`
  - `CERT_PASSWORD`
- 若未设置上述变量，将回退读取：
  - `ASPNETCORE_Kestrel__Certificates__Default__Path`
  - `ASPNETCORE_Kestrel__Certificates__Default__Password`
- 示例：
  ```bash
  export CERT_PATH=/etc/ssl/kccoding.top/site.pfx
  export CERT_PASSWORD=请替换为真实密码
  dotnet run --configuration Release
  ```
- 启动日志会输出证书路径与监听端口，便于确认配置是否生效。未找到证书或密码时，程序会给出明确的中文错误信息。

### 3. Linux 低端口绑定权限
Linux 上监听 80/443 需要 root 权限或为 `dotnet` 可执行文件授予特权：
```bash
sudo setcap 'cap_net_bind_service=+ep' $(readlink -f $(which dotnet))
```
执行后无需使用 root 即可绑定低位端口。

### 4. 日志与排查
- 启动成功后可在日志中看到“生产环境启动……”并显示证书路径。
- 每次 WebSocket 建立连接、接收请求、发送响应和关闭均会输出日志，便于排查问题。

## WebSocket 消息约定
- 客户端 → 服务端
  ```json
  {
    "type": "RequestCode",
    "email": "user@example.com"
  }
  ```
- 服务端 → 客户端
  ```json
  {
    "type": "CodeSent",
    "success": true,
    "message": "验证码已发送（模拟）"
  }
  ```
  当邮箱格式非法时，`success` 为 `false`，`message` 提示错误原因。

## 小程序上线配置
1. 在微信公众平台“小程序管理后台 → 开发 → 开发管理 → 开发设置”中，将 `wss://kccoding.top` 添加到 “socket 合法域名”。
2. 后端已在 `/wechat/ws` 暴露与 `/ws` 相同的 WebSocket 逻辑，上线时只需使用 `wss://kccoding.top/wechat/ws`。
3. 首次发布必须使用 HTTPS/WSS 证书；路径前缀 `/wechat` 由 Kestrel 服务端直接提供，无需额外反向代理。

## 注意事项
- 示例后端仅用于演示，不包含数据库或真实验证码发送逻辑。
- 如需扩展真实邮件服务、日志或监控，可在现有骨架上继续开发。
- 小程序在真机调试或上线前，请确认已切换至支持 `wss://` 的生产环境并完成合法域名配置。
