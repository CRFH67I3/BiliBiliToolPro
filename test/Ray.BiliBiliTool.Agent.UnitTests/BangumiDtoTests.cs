using System.Text.Json;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.ApiApi.Video;
using Xunit;

namespace Ray.BiliBiliTool.Agent.UnitTests;

/// <summary>
/// 番剧详情接口的反序列化测试。
/// 该接口会一次返回整季剧集（名侦探柯南有 1200+ 集），只要有一个字段溢出，
/// 整段 JSON 就会解析失败，表现为「未取到可观看的剧集」，所以这里用真实数值盯住类型宽度。
/// </summary>
public class BangumiDtoTests
{
    [Fact]
    public void Deserialize_KeepsIdsThatExceedInt32()
    {
        // 取自 https://api.bilibili.com/pgc/view/web/season?season_id=33378 的真实片段
        const string json = """
            {
              "code": 0,
              "message": "success",
              "result": {
                "episodes": [
                  {
                    "aid": 710444604,
                    "bvid": "BV1PQ4y1N7V8",
                    "cid": 26835486927,
                    "duration": 1493033,
                    "ep_id": 321808,
                    "id": 321808,
                    "long_title": "云霄飞车杀人事件",
                    "share_copy": "《名侦探柯南》第1话 云霄飞车杀人事件",
                    "status": 2
                  },
                  {
                    "aid": 3000000000,
                    "bvid": "BV1kR4y1t7aF",
                    "cid": 26772178176,
                    "duration": 1488653,
                    "ep_id": 321809,
                    "id": 321809,
                    "long_title": "董事长千金绑架事件",
                    "share_copy": "《名侦探柯南》第2话 董事长千金绑架事件",
                    "status": 2
                  }
                ]
              }
            }
            """;

        // Refit 的 SystemTextJsonContentSerializer 默认用 JsonSerializerDefaults.Web
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var res = JsonSerializer.Deserialize<GetBangumiBySsidResponse>(json, options);

        Assert.NotNull(res);
        Assert.Equal(0, res!.Code);
        Assert.Equal(2, res.Result.episodes.Count);

        var first = res.Result.episodes[0];
        Assert.Equal(710444604L, first.aid);
        Assert.Equal(26835486927L, first.cid);
        Assert.Equal(321808L, first.ep_id);

        // 关键回归点：第 1193 集这类新投稿的 aid 已超过 int.MaxValue
        var second = res.Result.episodes[1];
        Assert.Equal(3000000000L, second.aid);
        Assert.Equal(26772178176L, second.cid);
        Assert.Equal(321809L, second.ep_id);
    }
}
