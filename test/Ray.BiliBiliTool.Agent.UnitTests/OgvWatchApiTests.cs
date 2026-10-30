using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.ApiApi.VipBigPoint;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Refit;
using Xunit;

namespace Ray.BiliBiliTool.Agent.UnitTests;

/// <summary>
/// 大会员积分「观看剧集内容」三段式链路的接口定义测试（全部离线，用假 Handler 捕获真实请求）
/// </summary>
public class OgvWatchApiTests
{
    private const string CookieStr = "DedeUserID=385941972; bili_jct=synthetic; SESSDATA=synthetic";

    [Fact]
    public async Task StartOgvWatch_PostsFormWithRefererAndParsesNestedTaskId()
    {
        using var handler = new CapturingHandler(
            """
            {"code":0,"message":"success","data":{"closeType":"close_win","showTime":"","watch_count_down_cfg":{"action":"url","complete_status_desc":"大积分已到账","count_down_status_desc":"看${time}获大积分","milliseconds":600000,"task_id":"4320003","token":"899fdd7411"}}}
            """
        );
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.bilibili.com"),
        };
        var api = RestService.For<IApiApi>(client);

        var response = await api.StartOgvWatchAsync(
            new StartOgvWatchRequest(321808, 33378, "csrf-token", "buvid-value"),
            CookieStr
        );

        Assert.Equal("/pgc/activity/deliver/material/receive", handler.LastUri!.AbsolutePath);
        Assert.Equal(
            "https://big.bilibili.com/mobile/bigPoint/task",
            handler.LastHeaders!["Referer"]
        );
        Assert.Equal("h5", handler.LastHeaders["native_api_from"]);
        Assert.Contains("synthetic", handler.LastHeaders["Cookie"]);

        var form = HttpUtility.ParseQueryString(handler.LastBody!);
        Assert.Equal("321808", form["ep_id"]);
        Assert.Equal("33378", form["season_id"]);
        Assert.Equal("8451100", form["build"]);
        Assert.Equal("csrf-token", form["csrf"]);
        Assert.Equal("buvid-value", form["buvid"]);

        // task_id/token 嵌在 watch_count_down_cfg 里，且是字符串
        Assert.Equal("4320003", response.Data!.watch_count_down_cfg!.task_id);
        Assert.Equal("899fdd7411", response.Data.watch_count_down_cfg.token);
        Assert.Equal(600000, response.Data.watch_count_down_cfg.milliseconds);
    }

    [Fact]
    public async Task CompleteOgvWatch_SendsDoubleSignatureAndAllSignedFields()
    {
        using var handler = new CapturingHandler("""{"code":0,"message":"0"}""");
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.bilibili.com"),
        };
        var api = RestService.For<IApiApi>(client);

        var request = new CompleteOgvWatchRequest("4320003", "899fdd7411");
        await api.CompleteOgvWatchAsync(request, CookieStr);

        Assert.Equal("/pgc/activity/deliver/task/complete", handler.LastUri!.AbsolutePath);
        Assert.Equal(
            "https://big.bilibili.com/mobile/bigPoint/task",
            handler.LastHeaders!["Referer"]
        );
        Assert.Equal("h5", handler.LastHeaders["native_api_from"]);

        var form = HttpUtility.ParseQueryString(handler.LastBody!);
        Assert.Equal("4320003", form["task_id"]);
        Assert.Equal("899fdd7411", form["token"]);
        Assert.Equal("1d8b6e7d45233436", form["appkey"]);
        Assert.Equal("8451100", form["build"]);

        // 双重签名：两个字段都存在、都是 32 位小写十六进制、且不相等
        Assert.Matches("^[0-9a-f]{32}$", form["task_sign"]);
        Assert.Matches("^[0-9a-f]{32}$", form["sign"]);
        Assert.NotEqual(form["task_sign"], form["sign"]);

        // 参与签名的字段必须真的发出去了，否则服务端重算签名必然失败
        foreach (
            var key in new[]
            {
                "c_locale",
                "channel",
                "disable_rcmd",
                "mobi_app",
                "platform",
                "s_locale",
                "statistics",
                "timestamp",
                "ts",
            }
        )
        {
            Assert.False(string.IsNullOrEmpty(form[key]), $"缺少签名字段 {key}");
        }
    }

    [Fact]
    public async Task ReceiveOgvWatch_SendsAppContextFieldsOtherwise6007000()
    {
        using var handler = new CapturingHandler("""{"code":0,"message":"0"}""");
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.bilibili.com"),
        };
        // 复现线上环境：HttpClient 上有全局的 Web UA，方法级 [Headers] 必须能覆盖它
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0"
        );
        var api = RestService.For<IApiApi>(client);

        await api.VipBigPointReceiveV2ForOgv(
            new OgvReceiveRequest { csrf = "csrf-token", buvid = "buvid-value" },
            CookieStr
        );

        Assert.Equal("/pgc/activity/score/task/receive/v2", handler.LastUri!.AbsolutePath);
        Assert.Equal(
            "https://big.bilibili.com/mobile/bigPoint/task",
            handler.LastHeaders!["Referer"]
        );
        Assert.Equal("h5", handler.LastHeaders["native_api_from"]);

        // 方法级 UA 必须独占，不能与 HttpClient 默认 UA 合并成两个值
        var ua = handler.LastHeaders["User-Agent"];
        Assert.Contains("BiliDroid/8.45.1", ua);
        Assert.DoesNotContain("Chrome/120", ua);

        var form = HttpUtility.ParseQueryString(handler.LastBody!);
        Assert.Equal("ogvwatchnew", form["taskCode"]);
        Assert.Equal("8451100", form["build"]);
        Assert.Equal("android", form["mobi_app"]);
        Assert.Equal("android", form["platform"]);
        Assert.Equal("csrf-token", form["csrf"]);
        Assert.Equal("buvid-value", form["buvid"]);
    }

    private sealed class CapturingHandler(string json) : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        public string? LastBody { get; private set; }

        public Dictionary<string, string> LastHeaders { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            LastUri = request.RequestUri;

            LastHeaders.Clear();
            foreach (var header in request.Headers)
            {
                LastHeaders[header.Key] = string.Join(",", header.Value);
            }

            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
                foreach (var header in request.Content.Headers)
                {
                    LastHeaders[header.Key] = string.Join(",", header.Value);
                }
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        }
    }
}
