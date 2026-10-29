namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.ApiApi.VipBigPoint;

/// <summary>
/// 开始观看剧集任务请求（/pgc/activity/deliver/material/receive）
/// </summary>
/// <remarks>
/// 实测：带 Cookie + referer 即可调用，不需要 access_key；
/// build 必传，缺失会被判为旧客户端（code 6007000）。
/// </remarks>
public class StartOgvWatchRequest(long epId, long seasonId, string csrf, string buvid)
{
    public long ep_id { get; } = epId;

    public long season_id { get; } = seasonId;

    public string activity_code { get; } = "";

    public string build { get; } = "8451100";

    public string mobi_app { get; } = "android";

    public string platform { get; } = "android";

    public string channel { get; } = Constants.Channel;

    public string c_locale { get; } = "zh_CN";

    public string s_locale { get; } = "zh_CN";

    public int disable_rcmd { get; } = 0;

    public string from_spmid { get; } = "activity.h5.0.0";

    public string spmid { get; } = "united.player-video-detail.0.0";

    public string buvid { get; } = buvid;

    public string csrf { get; } = csrf;
}
