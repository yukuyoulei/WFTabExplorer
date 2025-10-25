using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
var environment = builder.Environment;
CertificateSettings? certificateSettings = null;

if (!environment.IsDevelopment())
{
    certificateSettings = LoadCertificateSettings();
}

builder.WebHost.ConfigureKestrel(options =>
{
    if (environment.IsDevelopment())
    {
        options.Listen(IPAddress.Any, 6000);
    }
    else
    {
        options.Listen(IPAddress.Any, 80);
        options.Listen(IPAddress.Any, 443, listenOptions =>
        {
            if (certificateSettings is null)
            {
                throw new InvalidOperationException("证书配置缺失，无法启动 HTTPS。");
            }

            listenOptions.UseHttps(certificateSettings.Path, certificateSettings.Password);
        });
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.Logger.LogInformation("开发环境启动，监听地址: http://0.0.0.0:6000/ws");
}
else
{
    app.Logger.LogInformation("生产环境启动，监听地址: http://0.0.0.0:80 (重定向至 HTTPS) 与 https://0.0.0.0:443/wechat/ws，证书路径: {CertPath}", certificateSettings!.Path);
    app.UseHttpsRedirection();
}

app.UseWebSockets();

var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

MapWebSocketEndpoint(app, "/ws", emailRegex);
MapWebSocketEndpoint(app, "/wechat/ws", emailRegex);

app.MapGet("/", () => Results.Text("WebSocket 服务运行中", "text/plain"));

app.Run();

static CertificateSettings LoadCertificateSettings()
{
    var certPath = Environment.GetEnvironmentVariable("CERT_PATH") ??
                   Environment.GetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Path");

    if (string.IsNullOrWhiteSpace(certPath))
    {
        throw new InvalidOperationException("未配置证书路径，请设置 CERT_PATH 或 ASPNETCORE_Kestrel__Certificates__Default__Path 环境变量。");
    }

    if (!File.Exists(certPath))
    {
        throw new FileNotFoundException($"证书文件不存在: {certPath}");
    }

    var certPassword = Environment.GetEnvironmentVariable("CERT_PASSWORD") ??
                       Environment.GetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Password");

    if (string.IsNullOrWhiteSpace(certPassword))
    {
        throw new InvalidOperationException("未配置证书密码，请设置 CERT_PASSWORD 或 ASPNETCORE_Kestrel__Certificates__Default__Password 环境变量。");
    }

    return new CertificateSettings(certPath, certPassword);
}

static void MapWebSocketEndpoint(WebApplication app, string path, Regex emailRegex)
{
    app.Map(path, async context =>
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var logger = app.Logger;
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var buffer = new byte[4096];
        var cancellationToken = context.RequestAborted;
        var connectionId = context.TraceIdentifier;

        logger.LogInformation("WebSocket 连接已建立 Path = {Path}, ConnectionId = {ConnectionId}", path, connectionId);

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                using var messageStream = new MemoryStream();
                var unsupportedType = false;
                WebSocketReceiveResult result;

                do
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        logger.LogInformation("客户端请求关闭 Path = {Path}, ConnectionId = {ConnectionId}", path, connectionId);
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "客户端关闭", cancellationToken);
                        return;
                    }

                    if (result.MessageType != WebSocketMessageType.Text)
                    {
                        unsupportedType = true;
                        await SendResponseAsync(webSocket, new ResponseMessage
                        {
                            Success = false,
                            Message = "仅支持文本消息"
                        }, cancellationToken, logger, path, connectionId);
                        break;
                    }

                    messageStream.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (unsupportedType || messageStream.Length == 0)
                {
                    continue;
                }

                var incoming = Encoding.UTF8.GetString(messageStream.ToArray());
                logger.LogInformation("收到消息 Path = {Path}, ConnectionId = {ConnectionId}, Payload = {Payload}", path, connectionId, incoming);

                RequestMessage? request;
                try
                {
                    request = JsonSerializer.Deserialize<RequestMessage>(incoming, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "JSON 反序列化失败 Path = {Path}, ConnectionId = {ConnectionId}", path, connectionId);
                    await SendResponseAsync(webSocket, new ResponseMessage
                    {
                        Success = false,
                        Message = "请求格式错误"
                    }, cancellationToken, logger, path, connectionId);
                    continue;
                }

                if (!string.Equals(request?.Type, "RequestCode", StringComparison.OrdinalIgnoreCase))
                {
                    await SendResponseAsync(webSocket, new ResponseMessage
                    {
                        Success = false,
                        Message = "未知请求类型"
                    }, cancellationToken, logger, path, connectionId);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(request?.Email) || !emailRegex.IsMatch(request.Email))
                {
                    await SendResponseAsync(webSocket, new ResponseMessage
                    {
                        Success = false,
                        Message = "邮箱格式不正确"
                    }, cancellationToken, logger, path, connectionId);
                    continue;
                }

                await SendResponseAsync(webSocket, new ResponseMessage
                {
                    Success = true,
                    Message = "验证码已发送（模拟）"
                }, cancellationToken, logger, path, connectionId);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("请求被取消 Path = {Path}, ConnectionId = {ConnectionId}", path, connectionId);
        }
        catch (WebSocketException ex)
        {
            logger.LogWarning(ex, "WebSocket 通信异常 Path = {Path}, ConnectionId = {ConnectionId}", path, connectionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WebSocket 处理出现未捕获异常 Path = {Path}, ConnectionId = {ConnectionId}", path, connectionId);
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "服务器异常", cancellationToken);
            }
        }
        finally
        {
            logger.LogInformation("连接结束 Path = {Path}, ConnectionId = {ConnectionId}", path, connectionId);
        }
    });
}

static async Task SendResponseAsync(WebSocket socket, ResponseMessage response, CancellationToken cancellationToken, ILogger logger, string path, string connectionId)
{
    response.Type = "CodeSent";
    var payload = JsonSerializer.Serialize(response);
    logger.LogInformation("发送响应 Path = {Path}, ConnectionId = {ConnectionId}, Payload = {Payload}", path, connectionId, payload);
    await socket.SendAsync(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, true, cancellationToken);
}

sealed record RequestMessage
{
    public string? Type { get; init; }
    public string? Email { get; init; }
}

sealed record ResponseMessage
{
    public string Type { get; set; } = "CodeSent";
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

sealed record CertificateSettings(string Path, string Password);
