namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.ApiApi.VipBigPoint;

/// <summary>
/// 「观看剧集内容」(ogvwatchnew) 专用的领取请求。
/// </summary>
/// <remarks>
/// 该任务与其它日常任务不同：只带 taskCode + build 会被服务端按「旧客户端」拒绝，
/// 返回 6007000「请将客户端更新至最新版本」，而同一接口的其它任务码却正常。
/// 补齐 App 侧的上下文字段（mobi_app / platform / csrf / buvid）后才会通过。
/// </remarks>
public class OgvReceiveRequest
{
    public string taskCode { get; set; } = "ogvwatchnew";

    /// <summary>客户端版本号，服务端按此判断版本是否过低</summary>
    public string build { get; set; } = "8451100";

    public string mobi_app { get; set; } = "android";

    public string platform { get; set; } = "android";

    public string csrf { get; set; } = "";

    public string buvid { get; set; } = "";
}
