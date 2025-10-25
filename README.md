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

## 本地运行步骤
### 1. 启动后端（ASP.NET Core WebSocket）
1. 安装 [.NET SDK 8](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0)。
2. 打开终端并执行：
   ```bash
   cd server/aspnetcore-ws
   dotnet restore
   dotnet run
   ```
3. 默认监听地址为 `http://localhost:5000`，WebSocket 端点为 `ws://localhost:5000/ws`。终端会输出收到的请求与返回的结果。

### 2. 运行微信小程序客户端
1. 安装 [微信开发者工具](https://developers.weixin.qq.com/miniprogram/dev/devtools/download.html)。
2. 打开微信开发者工具，选择“导入项目”，项目目录指向 `client/miniprogram`。
3. 使用测试号时可保持 `project.config.json` 内的 `appid` 为 `touristappid`，实际项目请替换为有效 AppID。
4. 在“详情”设置中关闭“校验合法域名、web-view（业务域名）、TLS 版本以及 HTTPS 证书”，以便本地调试 WebSocket。
5. 编译并预览小程序：
   - 输入有效邮箱后按钮会变为可点击。
   - 点击“获取验证码”即向后端发送 JSON 请求。
   - 收到响应后会弹出 Toast 并在页面下方展示状态。

## 配置项说明
- WebSocket 地址在 `client/miniprogram/config.js` 中配置：
  ```js
  const WS_URL = 'ws://localhost:5000/ws';
  ```
  部署至其他环境时修改此值即可，建议与后端部署地址保持一致。
- WebSocket 工具封装位于 `client/miniprogram/utils/ws.js`，包含自动重连、消息队列与事件订阅等基础能力，可根据业务需求扩展。

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

## 后续可拓展方向（非本次范围）
- 接入真实的邮件服务（SMTP 或第三方 API），发送并校验验证码。
- 使用 HTTPS / WSS 与域名白名单，满足微信小程序上线要求。
- 对接云托管或容器化部署脚本，对接日志与监控系统。
- 增加验证码倒计时、请求频率限制与持久化日志。

## 注意事项
- 示例后端仅用于演示，不包含数据库或真实验证码发送逻辑。
- 小程序在真机调试或上线前，请将 WebSocket 地址切换为支持 `wss://` 的生产环境并在微信公众平台配置合法域名。
