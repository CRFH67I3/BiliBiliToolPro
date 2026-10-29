namespace Ray.BiliBiliTool.Config.Options;

public class VipBigPointOptions : BaseConfigOptions
{
    public override string SectionName => "VipBigPointConfig";

    public string? ViewBangumis { get; set; }

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
                { $"{SectionName}:{nameof(OgvWatchWaitSeconds)}", OgvWatchWaitSeconds.ToString() },
            }
        );
    }
}
