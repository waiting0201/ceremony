using System.Text.Encodings.Web;
using System.Text.Json;
using Ceremony.Domain.Exceptions;

namespace Ceremony.Api.Middleware;

public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    // 用 UnsafeRelaxedJsonEscaping 讓繁中錯誤訊息以 UTF-8 直接輸出（不 escape 成 \uXXXX），
    // 方便前端比對「verbatim 中文錯誤訊息」與舊系統 MessageBox 對齊。
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (DomainException ex)
        {
            ctx.Response.StatusCode = MapStatus(ex.ErrorCode);
            ctx.Response.ContentType = "application/json; charset=utf-8";
            var payload = new
            {
                errorCode = ex.ErrorCode,
                message = ex.Message,
                traceId = ctx.TraceIdentifier,
            };
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOpts));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            var payload = new
            {
                errorCode = "INTERNAL_ERROR",
                message = "未預期的伺服器錯誤",
                traceId = ctx.TraceIdentifier,
            };
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOpts));
        }
    }

    private static int MapStatus(string errorCode) => errorCode switch
    {
        "AUTH_INVALID_CREDENTIALS" => StatusCodes.Status401Unauthorized,
        "AUTH_ACCOUNT_LOCKED" => StatusCodes.Status423Locked,
        "ADMIN_DUPLICATE_USERNAME" => StatusCodes.Status409Conflict,
        "BELIEVER_NOT_FOUND" => StatusCodes.Status404NotFound,
        "BELIEVER_HAS_SIGNUPS" => StatusCodes.Status409Conflict,
        "SIGNUP_NOT_FOUND" => StatusCodes.Status404NotFound,
        "SIGNUP_NUMBER_CONFLICT" => StatusCodes.Status409Conflict,
        "CATEGORY_HAS_DEPENDENCY" => StatusCodes.Status409Conflict,
        "CATEGORY_DEPTH_LIMIT" => StatusCodes.Status422UnprocessableEntity,
        "WORSHIP_ONLY_TYPE_4" => StatusCodes.Status422UnprocessableEntity,
        "BATCH_NO_SIGNUPS" => StatusCodes.Status404NotFound,
        "BACKUP_NOT_CONFIGURED" => StatusCodes.Status500InternalServerError,
        "INTERNAL_ERROR" => StatusCodes.Status500InternalServerError,
        _ when errorCode.EndsWith("_NOT_FOUND") => StatusCodes.Status404NotFound,
        _ when errorCode.StartsWith("AUTH_") => StatusCodes.Status401Unauthorized,
        _ when errorCode.StartsWith("VALIDATION_") => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status409Conflict,
    };
}
