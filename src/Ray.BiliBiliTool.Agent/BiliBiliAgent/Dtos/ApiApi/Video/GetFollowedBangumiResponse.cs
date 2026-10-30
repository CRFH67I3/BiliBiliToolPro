namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.ApiApi.Video;

/// <summary>
/// 「我的追番」列表（/x/space/bangumi/follow/list）响应。
/// </summary>
/// <remarks>
/// 该接口用 Cookie 即可访问，不需要 WBI 签名，也不需要 access_key。
/// 单条数据里已经带了 <see cref="FollowedBangumiItem.first_ep"/>（第 1 话的 ep_id）
/// 和 <see cref="FollowedBangumiItem.season_id"/>，取一集看不需要再拉整季详情。
/// </remarks>
public class GetFollowedBangumiResponse
{
    public List<FollowedBangumiItem> List { get; set; } = [];

    public int Total { get; set; }
}

public class FollowedBangumiItem
{
    public long season_id { get; set; }

    public string? title { get; set; }

    /// <summary>观看进度，例如「看到第11话 7:10」「已看完第21话」「看到全片 0:55」</summary>
    public string? progress { get; set; }

    /// <summary>1 想看 / 2 在看 / 3 看过</summary>
    public int follow_status { get; set; }

    /// <summary>第 1 话的 ep_id。0 表示没有正片（未开播等），需要跳过。</summary>
    public long first_ep { get; set; }

    public FollowedBangumiNewEp? new_ep { get; set; }
}

public class FollowedBangumiNewEp
{
    public long id { get; set; }

    /// <summary>例如「全12话」</summary>
    public string? index_show { get; set; }

    public string? long_title { get; set; }
}
