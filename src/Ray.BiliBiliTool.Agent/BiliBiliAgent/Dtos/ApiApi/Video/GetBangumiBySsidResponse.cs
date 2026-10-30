namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.ApiApi.Video;

public class GetBangumiBySsidResponse
{
    public int Code { get; set; } = int.MinValue;

    public string? Message { get; set; }

    public required Result Result { get; set; }
}

public class Result
{
    public List<Episode> episodes { get; set; } = [];
}

public class Episode
{
    public int aid { get; set; }

    public required string bvid { get; set; }

    /// <summary>
    /// 稿件 cid。注意不能是 int：2024 年后新稿件的 cid 已超过 int.MaxValue，
    /// 例如 名侦探柯南 ep321808 的 cid 为 26835486927，用 int 会导致整段 JSON 反序列化失败。
    /// </summary>
    public long cid { get; set; }

    public int duration { get; set; }

    public int ep_id { get; set; }

    public int id { get; set; }

    public required string long_title { get; set; }

    public required string share_copy { get; set; }

    public int status { get; set; }
}
