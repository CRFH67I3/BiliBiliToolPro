using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Refit;
using Xunit;

namespace Ray.BiliBiliTool.Agent.UnitTests;

/// <summary>
/// 「我的追番」列表接口测试（离线，用假 Handler 捕获真实请求）。
/// 该接口是「观看剧集内容」挑选剧集的新数据源，只依赖 Cookie，不需要 WBI 签名。
/// </summary>
public class FollowedBangumiTests
{
    private const string CookieStr = "DedeUserID=385941972; bili_jct=synthetic; SESSDATA=synthetic";

    private const string SampleJson = """
        {
          "code": 0,
          "message": "0",
          "ttl": 1,
          "data": {
            "list": [
              {
                "season_id": 48143,
                "season_type": 4,
                "title": "时光代理人 英都篇",
                "progress": "看到第1话 13:43",
                "follow_status": 3,
                "first_ep": 1222558,
                "total_count": 6,
                "new_ep": { "id": 1222563, "index_show": "全6话", "long_title": "迷局" }
              },
              {
                "season_id": 39707,
                "season_type": 4,
                "title": "中国奇谭",
                "progress": "看到第1话 1:23",
                "follow_status": 3,
                "first_ep": 706666,
                "total_count": 8
              }
            ],
            "total": 62
          }
        }
        """;

    [Fact]
    public async Task GetFollowedBangumiList_SendsExpectedQueryAndParsesItems()
    {
        using var handler = new CapturingHandler(SampleJson);
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.bilibili.com"),
        };
        var api = RestService.For<IApiApi>(client);

        var res = await api.GetFollowedBangumiList(385941972, CookieStr);

        Assert.Equal("/x/space/bangumi/follow/list", handler.LastUri!.AbsolutePath);

        // follow_status=3 表示「看过」，type=1 表示番剧
        var query = handler.LastUri.Query;
        Assert.Contains("type=1", query);
        Assert.Contains("follow_status=3", query);
        Assert.Contains("vmid=385941972", query);
        Assert.Contains("synthetic", handler.LastHeaders!["Cookie"]);

        Assert.Equal(0, res.Code);
        Assert.NotNull(res.Data);
        Assert.Equal(62, res.Data!.Total);
        Assert.Equal(2, res.Data.List.Count);

        var first = res.Data.List[0];
        Assert.Equal(48143L, first.season_id);
        Assert.Equal(1222558L, first.first_ep);
        Assert.Equal("看到第1话 13:43", first.progress);
        Assert.Equal(3, first.follow_status);
        Assert.Equal(1222563L, first.new_ep!.id);

        // new_ep 缺失时不能抛异常（真实数据里部分条目没有该字段）
        var second = res.Data.List[1];
        Assert.Equal(39707L, second.season_id);
        Assert.Null(second.new_ep);
    }

    private sealed class CapturingHandler(string json) : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        public Dictionary<string, string>? LastHeaders { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            LastUri = request.RequestUri;
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers)
                headers[header.Key] = string.Join(",", header.Value);

            LastHeaders = headers;

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        json,
                        System.Text.Encoding.UTF8,
                        "application/json"
                    ),
                }
            );
        }
    }
}
