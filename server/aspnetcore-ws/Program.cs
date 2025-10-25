using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    var logger = app.Logger;
    var buffer = new byte[4096];
    var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    var cancellationToken = context.RequestAborted;

    logger.LogInformation("WebSocket 连接已建立: {ConnectionId}", context.TraceIdentifier);

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
                    logger.LogInformation("客户端请求关闭: {ConnectionId}", context.TraceIdentifier);
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
                    }, cancellationToken, logger);
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
            logger.LogInformation("收到消息: {Payload}", incoming);

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
                logger.LogWarning(ex, "JSON 反序列化失败");
                await SendResponseAsync(webSocket, new ResponseMessage
                {
                    Success = false,
                    Message = "请求格式错误"
                }, cancellationToken, logger);
                continue;
            }

            if (!string.Equals(request?.Type, "RequestCode", StringComparison.OrdinalIgnoreCase))
            {
                await SendResponseAsync(webSocket, new ResponseMessage
                {
                    Success = false,
                    Message = "未知请求类型"
                }, cancellationToken, logger);
                continue;
            }

            if (string.IsNullOrWhiteSpace(request?.Email) || !emailRegex.IsMatch(request.Email))
            {
                await SendResponseAsync(webSocket, new ResponseMessage
                {
                    Success = false,
                    Message = "邮箱格式不正确"
                }, cancellationToken, logger);
                continue;
            }

            await SendResponseAsync(webSocket, new ResponseMessage
            {
                Success = true,
                Message = "验证码已发送（模拟）"
            }, cancellationToken, logger);
        }
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("请求被取消: {ConnectionId}", context.TraceIdentifier);
    }
    catch (WebSocketException ex)
    {
        logger.LogWarning(ex, "WebSocket 通信异常: {ConnectionId}", context.TraceIdentifier);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "WebSocket 处理出现未捕获异常: {ConnectionId}", context.TraceIdentifier);
        if (webSocket.State == WebSocketState.Open)
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "服务器异常", cancellationToken);
        }
    }
    finally
    {
        logger.LogInformation("连接结束: {ConnectionId}", context.TraceIdentifier);
    }
});

app.MapGet("/", () => Results.Text("WebSocket 服务运行中", "text/plain"));

app.Run("http://0.0.0.0:5000");

static async Task SendResponseAsync(WebSocket socket, ResponseMessage response, CancellationToken cancellationToken, ILogger logger)
{
    response.Type = "CodeSent";
    var payload = JsonSerializer.Serialize(response);
    logger.LogInformation("发送响应: {Payload}", payload);
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
