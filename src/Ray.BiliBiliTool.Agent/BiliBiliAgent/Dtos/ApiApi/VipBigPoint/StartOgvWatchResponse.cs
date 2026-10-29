namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.ApiApi.VipBigPoint;

/// <summary>
/// 开始观看剧集任务响应
/// </summary>
/// <remarks>
/// task_id 与 token 实际嵌在 data.watch_count_down_cfg 里，且均为字符串。
/// </remarks>
public class StartOgvWatchResponse
{
    public string? closeType { get; set; }

    public string? showTime { get; set; }

    public WatchCountDownCfg? watch_count_down_cfg { get; set; }
}

public class WatchCountDownCfg
{
    /// <summary>
    /// 本次观看任务标识，上报完成时回传
    /// </summary>
    public string? task_id { get; set; }

    /// <summary>
    /// 本次观看任务令牌，上报完成时回传
    /// </summary>
    public string? token { get; set; }

    /// <summary>
    /// 服务端倒计时（毫秒），实测 600000 即 10 分钟
    /// </summary>
    public long milliseconds { get; set; }

    public string? complete_status_desc { get; set; }

    public string? count_down_status_desc { get; set; }
}
