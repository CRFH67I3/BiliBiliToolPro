using System.Collections.Generic;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Utils;
using Xunit;

namespace Ray.BiliBiliTool.Agent.UnitTests;

/// <summary>
/// App 签名算法测试。期望值由独立实现（Python/encodeURIComponent + MD5）算出，
/// 避免用同一份代码自我验证。
/// </summary>
public class AppSignerTests
{
    private static Dictionary<string, string> BuildFixedParameters() =>
        new()
        {
            ["appkey"] = AppSigner.AndroidAppKey,
            ["build"] = "8451100",
            ["c_locale"] = "zh_CN",
            ["channel"] = "bili",
            ["disable_rcmd"] = "0",
            ["mobi_app"] = "android",
            ["platform"] = "android",
            ["s_locale"] = "zh_CN",
            ["statistics"] = "{\"appId\":1,\"platform\":3,\"version\":\"8.45.1\",\"abtest\":\"\"}",
            ["task_id"] = "4320003",
            ["timestamp"] = "1748884714621",
            ["token"] = "67ba5888e7",
            ["ts"] = "1748884714",
        };

    [Fact]
    public void Sign_FixedParameters_MatchesIndependentImplementation()
    {
        var parameters = BuildFixedParameters();

        // 第一次签名：task_sign（排除 sign 与 task_sign）
        string taskSign = AppSigner.Sign(parameters);
        Assert.Equal("56fbde07af485f9bfea13073c309e3eb", taskSign);

        // 第二次签名：带上 task_sign 得到 sign
        parameters["task_sign"] = taskSign;
        string sign = AppSigner.Sign(parameters);
        Assert.Equal("4165151861b95cacecc7ba32b9a70f9f", sign);
    }

    [Fact]
    public void Sign_IsOrderIndependent()
    {
        var forward = new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" };
        var backward = new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" };

        Assert.Equal(AppSigner.Sign(forward), AppSigner.Sign(backward));
    }
}
