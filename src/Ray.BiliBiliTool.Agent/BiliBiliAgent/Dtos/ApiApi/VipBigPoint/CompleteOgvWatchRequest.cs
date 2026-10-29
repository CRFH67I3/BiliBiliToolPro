using System.Globalization;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Utils;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.ApiApi.VipBigPoint;

/// <summary>
/// 上报观看剧集完成请求（/pgc/activity/deliver/task/complete）
/// </summary>
/// <remarks>
/// 该接口需要 App 双重签名：
/// 1) 排除 task_sign 与 sign 签名一次得到 task_sign；
/// 2) 带上 task_sign 再签一次得到 sign。
/// 且同一 task_id 只能成功上报一次，重复调用返回 400。
/// 实测可用 Cookie 鉴权，不需要 access_key。
/// </remarks>
public class CompleteOgvWatchRequest
{
    private static readonly string[] SignExcludedKeys = ["sign", "task_sign"];

    public CompleteOgvWatchRequest(string taskId, string token)
    {
        task_id = taskId;
        this.token = token;
        ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        timestamp = DateTimeOffset
            .UtcNow.ToUnixTimeMilliseconds()
            .ToString(CultureInfo.InvariantCulture);

        // 先签 task_sign，再带 task_sign 签 sign
        var parameters = ToSignDictionary();
        string taskSign = AppSigner.Sign(
            parameters
                .Where(x => !SignExcludedKeys.Contains(x.Key))
                .ToDictionary(x => x.Key, x => x.Value)
        );

        parameters["task_sign"] = taskSign;
        string signValue = AppSigner.Sign(parameters);

        task_sign = taskSign;
        sign = signValue;
    }

    public string appkey { get; } = AppSigner.AndroidAppKey;

    public string build { get; } = "8451100";

    public string c_locale { get; } = "zh_CN";

    public string channel { get; } = Constants.Channel;

    public int disable_rcmd { get; } = 0;

    public string mobi_app { get; } = "android";

    public string platform { get; } = "android";

    public string s_locale { get; } = "zh_CN";

    public string statistics { get; } =
        "{\"appId\":1,\"platform\":3,\"version\":\"8.45.1\",\"abtest\":\"\"}";

    public string task_id { get; }

    public string timestamp { get; }

    public string token { get; }

    public string ts { get; }

    public string task_sign { get; }

    public string sign { get; }

    /// <summary>
    /// 参与签名的参数集合，必须与实际提交的表单字段完全一致
    /// </summary>
    private Dictionary<string, string> ToSignDictionary() =>
        new()
        {
            ["appkey"] = appkey,
            ["build"] = build,
            ["c_locale"] = c_locale,
            ["channel"] = channel,
            ["disable_rcmd"] = disable_rcmd.ToString(CultureInfo.InvariantCulture),
            ["mobi_app"] = mobi_app,
            ["platform"] = platform,
            ["s_locale"] = s_locale,
            ["statistics"] = statistics,
            ["task_id"] = task_id,
            ["timestamp"] = timestamp,
            ["token"] = token,
            ["ts"] = ts,
        };
}
