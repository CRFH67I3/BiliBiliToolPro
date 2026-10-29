using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Utils;

/// <summary>
/// B 站 App 端接口签名（appkey + appsec，MD5）
/// </summary>
/// <remarks>
/// 算法：参数按 key 升序排列，key 与 value 均做 encodeURIComponent，
/// 拼成 a=1&amp;b=2 后追加 appsec，取 MD5 小写十六进制。
/// 双重签名的接口（如观看剧集上报）需先排除 task_sign 与 sign 签一次得到 task_sign，
/// 再把 task_sign 加入参数签第二次得到 sign。
/// </remarks>
public static class AppSigner
{
    /// <summary>
    /// 安卓客户端 appkey（社区公开值，长期未变）
    /// </summary>
    public const string AndroidAppKey = "1d8b6e7d45233436";

    /// <summary>
    /// 安卓客户端 appsec
    /// </summary>
    public const string AndroidAppSec = "560c52ccd288fed045859ed18bffd973";

    public static string Sign(IReadOnlyDictionary<string, string> parameters)
    {
        string query = string.Join(
            "&",
            parameters
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{Encode(x.Key)}={Encode(x.Value)}")
        );

        return Md5(query + AndroidAppSec);
    }

    private static string Encode(string? value) => Uri.EscapeDataString(value ?? string.Empty);

    private static string Md5(string text)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(text));
        StringBuilder sb = new(hash.Length * 2);
        foreach (byte b in hash)
        {
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }
}
