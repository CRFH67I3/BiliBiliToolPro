namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.ApiApi.VipBigPoint;

public class ReceiveOrCompleteTaskRequest
{
    public ReceiveOrCompleteTaskRequest(string taskCode)
    {
        TaskCode = taskCode;
    }

    public string TaskCode { get; set; }

    /// <summary>
    /// 客户端版本号。缺失时服务端按旧客户端处理，
    /// 大会员积分任务会返回 code 6007000「请将客户端更新至最新版本」
    /// </summary>
    public string build { get; } = "8451100";
}
