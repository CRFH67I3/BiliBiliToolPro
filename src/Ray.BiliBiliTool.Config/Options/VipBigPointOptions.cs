namespace Ray.BiliBiliTool.Config.Options;

public class VipBigPointOptions : BaseConfigOptions
{
    public override string SectionName => "VipBigPointConfig";

    public string? ViewBangumis { get; set; }

    /// <summary>
    /// 是否优先从「我的追番」里挑一部看过的剧集来完成观看任务。
    /// </summary>
    /// <remarks>
    /// 默认开启。追番列表里的第 1 话必然是看过的、可播放的，比写死某一部番剧更稳，
    /// 也不必拉取整季详情（名侦探柯南一季 1200+ 集，响应 1.2MB，字段还容易溢出）。
    /// 追番为空或接口失败时自动回退到 <see cref="ViewBangumis"/> 配置的番剧。
    /// </remarks>
    public bool UseFollowedBangumis { get; set; } = true;

    /// <summary>
    /// 观看剧集任务开始后需等待的秒数。
    /// 服务端倒计时固定下发 600 秒，返回里没有倒计时信息时用该值兜底。
    /// </summary>
    public int OgvWatchWaitSeconds { get; set; } = 610;

    public List<long> ViewBangumiList
    {
        get
        {
            List<long> re = [];
            if (string.IsNullOrWhiteSpace(ViewBangumis) | ViewBangumis == "-1")
                return re;

            string[] array = ViewBangumis?.Split(',') ?? [];
            foreach (string item in array)
            {
                if (long.TryParse(item.Trim(), out long upId))
                    re.Add(upId);
                else
                    re.Add(long.MinValue);
            }
            return re;
        }
    }

    public override Dictionary<string, string> ToConfigDictionary()
    {
        return MergeConfigDictionary(
            new Dictionary<string, string>
            {
                { $"{SectionName}:{nameof(ViewBangumis)}", ViewBangumis ?? "" },
                { $"{SectionName}:{nameof(UseFollowedBangumis)}", UseFollowedBangumis.ToString() },
                { $"{SectionName}:{nameof(OgvWatchWaitSeconds)}", OgvWatchWaitSeconds.ToString() },
            }
        );
    }
}
