using Ceremony.Application.Backup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ceremony.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/backup")]
public sealed class BackupController(BackupHandler handler) : ControllerBase
{
    /// <summary>觸發 SQL Server BACKUP DATABASE</summary>
    /// <remarks>
    /// Legacy: MainForm.cs:95-113 (btnBackup_Click)
    /// 備份檔位於 appsettings `Backup:Directory`；檔名 `Ceremony-{yyyyMMddHHmmss}.bak`
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(BackupResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BackupResponse>> Backup([FromBody] BackupRequest? request, CancellationToken ct)
    {
        var result = await handler.HandleAsync(request ?? new BackupRequest(), ct);
        return Ok(result);
    }

    /// <summary>下載備份檔（.bak/.trn）供 client 端另存。</summary>
    /// <remarks>
    /// Blueprint: docs/blueprints/api-endpoints/get-backup-download.md
    /// 用途：sidecar 模式下 .bak 由 DB 主機端寫，瀏覽器無法選本機路徑；
    /// Electron 殼以原生「另存新檔」對話框接收此串流寫到 client 任意位置。
    /// 檔名 traversal 防護在 <c>SqlBackupService.IsValidBackupFileName</c>：
    /// 不合法 → 400 <c>VALIDATION_BACKUP_FILENAME</c>；找不到 / 不可讀 → 404 <c>BACKUP_FILE_NOT_FOUND</c>。
    /// </remarks>
    [HttpGet("{fileName}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Download(string fileName)
    {
        var file = handler.OpenFile(fileName);
        // FileStreamResult 會在回應結束後自動 dispose 串流；CanSeek=true 時帶 Content-Length。
        return File(file.Content, "application/octet-stream", file.FileName, enableRangeProcessing: true);
    }
}
