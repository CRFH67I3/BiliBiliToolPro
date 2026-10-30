using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ray.BiliBiliTool.Agent;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.ApiApi.Daily;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.ApiApi.Video;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.ApiApi.VipBigPoint;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.ApiApi.VipBigPoint.ThreeDaysSign;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Dtos.ShowApi;
using Ray.BiliBiliTool.Agent.BiliBiliAgent.Interfaces;
using Ray.BiliBiliTool.Config.Options;
using Ray.BiliBiliTool.Domain.Exceptions;
using Ray.BiliBiliTool.DomainService.Dtos;
using Ray.BiliBiliTool.DomainService.Interfaces;

namespace Ray.BiliBiliTool.DomainService;

public class VipBigPointDomainService(
    ILogger<VipBigPointDomainService> logger,
    IOptionsMonitor<VipBigPointOptions> vipBigPointOptions,
    IShowApi showApi,
    IApiApi apiApi,
    IAccountDomainService accountDomainService,
    IVideoDomainService videoDomainService
) : IVipBigPointDomainService
{
    private readonly VipBigPointOptions _vipBigPointOptions = vipBigPointOptions.CurrentValue;

    public async Task<VipBigPointCombine> GetCombineAsync(BiliCookie ck)
    {
        var allTasks = await apiApi.GetCombineAsync(
            new GetCombineRequest { csrf = ck.BiliJct, buvid = ck.Buvid },
            ck.ToString()
        );
        if (allTasks.Code != 0 || allTasks.Data is null)
            throw new BiliBusinessException(allTasks.ToJsonStr());
        return allTasks.Data;
    }

    /// <summary>
    /// 领取大会员专属等级加速包
    /// </summary>
    public async Task VipExpressAsync(BiliCookie ck)
    {
        var re = await apiApi.GetVouchersInfoAsync(ck.ToString());
        if (re.Code == 0 && re.Data is not null)
        {
            var state = re.Data.List.Find(x => x.Type == 9)?.State;

            switch (state)
            {
                case 2:
                    logger.LogInformation("大会员经验观看任务未完成");
                    logger.LogInformation("开始观看视频");
                    // 观看视频，暂时没有好办法解决，先这样使着
                    DailyTaskInfo dailyTaskInfo = await accountDomainService.GetDailyTaskStatus(ck);
                    await videoDomainService.WatchAndShareVideo(dailyTaskInfo, ck);
                    // 跳转到未兑换，执行兑换任务
                    goto case 0;

                case 1:
                    logger.LogInformation("大会员经验已兑换");
                    break;

                case 0:
                    logger.LogInformation("大会员经验未兑换");
                    //兑换api
                    var response = await apiApi.ObtainVipExperienceAsync(
                        new VipExperienceRequest { csrf = ck.BiliJct },
                        ck.ToString()
                    );
                    if (response.Code != 0)
                    {
                        logger.LogInformation(
                            "大会员经验领取失败，错误信息：{message}",
                            response.Message
                        );
                        break;
                    }

                    logger.LogInformation("领取成功，经验+10 √");
                    var combine = await GetCombineAsync(ck);
                    combine.LogPointInfo(logger);
                    break;

                default:
                    logger.LogDebug("大会员经验领取失败，未知错误");
                    break;
            }
        }
    }

    /// <summary>
    /// 签到
    /// </summary>
    /// <param name="ck"></param>
    /// <exception cref="Exception"></exception>
    public async Task SignAsync(BiliCookie ck)
    {
        var signInfo = await apiApi.GetThreeDaySignAsync(
            new ThreeDaySignRequest { csrf = ck.BiliJct },
            ck.ToString()
        );
        if (signInfo.Data?.three_day_sign.signed == true)
        {
            logger.LogInformation("已完成，跳过");
            logger.LogInformation(signInfo.Data.ToString());
            return;
        }

        BiliApiResponse<Sign2Response> re = await apiApi.Sign2Async(
            new Sign2RequestPath(ck.BiliJct),
            new Sign2Request(),
            ck.ToString()
        );
        if (re.Code != 0 || re.Data is null)
            throw new BiliBusinessException(re.ToJsonStr());

        logger.LogInformation("签到成功");
        logger.LogInformation(re.Data.ToString());

        signInfo = await apiApi.GetThreeDaySignAsync(
            new ThreeDaySignRequest { csrf = ck.BiliJct },
            ck.ToString()
        );
        signInfo.Data?.LogPointInfo(logger);
    }

    /// <summary>
    /// 领取任务
    /// </summary>
    /// <param name="combine"></param>
    /// <param name="ck"></param>
    public async Task ReceiveDailyMissionsAsync(VipBigPointCombine combine, BiliCookie ck)
    {
        const string moduleCode = "日常任务";

        var module = combine.Task_info.Modules.FirstOrDefault(x => x.module_title == moduleCode);
        var missionsNeedReceive = module?.common_task_item.Where(x => x.state == 0).ToList();
        if (missionsNeedReceive == null || missionsNeedReceive.Count == 0)
        {
            logger.LogInformation("均已领取，跳过");
            return;
        }

        foreach (var targetTask in missionsNeedReceive)
        {
            logger.LogInformation("开始领取任务：{task}", targetTask.title);
            await TryReceive(targetTask.task_code, ck);
        }
    }

    public async Task ReceiveAndCompleteAsync(
        VipBigPointCombine info,
        string moduleCode,
        string taskCode,
        BiliCookie ck,
        Func<string, BiliCookie, Task<bool>> completeFunc
    )
    {
        var module = info.Task_info.Modules.FirstOrDefault(x => x.module_title == moduleCode);
        var bonusTask = module?.common_task_item.FirstOrDefault(x => x.task_code == taskCode);

        if (bonusTask == null)
        {
            logger.LogInformation("任务失效");
            return;
        }

        if (bonusTask.state == 3)
        {
            logger.LogInformation("已完成，跳过");
            return;
        }

        if (bonusTask.state == 0)
        {
            logger.LogInformation("开始领取任务");
            await TryReceive(bonusTask.task_code, ck);
        }

        logger.LogInformation("开始完成任务");
        var re = await completeFunc(taskCode, ck);

        //确认
        if (re)
        {
            var combine = await GetCombineAsync(ck);
            module = combine.Task_info.Modules.FirstOrDefault(x => x.module_title == moduleCode);
            bonusTask = module?.common_task_item.FirstOrDefault(x => x.task_code == taskCode);
            var success = bonusTask is { state: 3, complete_times: >= 1 };
            logger.LogInformation("确认：{re}", success ? "成功，经验 +10" : "失败");
        }
    }

    public async Task<bool> CompleteAsync(string taskCode, BiliCookie ck)
    {
        var request = new ReceiveOrCompleteTaskRequest(taskCode);
        var re = await apiApi.VipBigPointCompleteAsync(request, ck.ToString());
        if (re.Code == 0)
        {
            logger.LogInformation("已完成");
            return true;
        }

        logger.LogInformation("失败：{msg}", re.ToJsonStr());
        return false;
    }

    public async Task<bool> CompleteViewAsync(string taskCode, BiliCookie ck)
    {
        var channel = taskCode switch
        {
            "animatetab" => "jp_channel",
            "filmtab" => "tv_channel",
            _ => throw new ArgumentOutOfRangeException(
                nameof(taskCode),
                $"Invalid taskCode: {taskCode}"
            ),
        };

        logger.LogInformation("开始浏览");
        await Task.Delay(10 * 1000);

        var request = new ViewRequest(channel);
        var re = await apiApi.VipBigPointViewComplete(request, ck.ToString());
        if (re.Code == 0)
        {
            logger.LogInformation("浏览完成");
            return true;
        }

        logger.LogInformation("浏览失败：{msg}", re.ToJsonStr());
        return false;
    }

    public async Task<bool> CompleteViewVipMallAsync(string taskCode, BiliCookie ck)
    {
        var re = await showApi.ViewVipMallAsync(
            new ViewVipMallRequest { Csrf = ck.BiliJct },
            ck.ToString()
        );
        if (re.Code != 0)
            throw new BiliBusinessException(re.ToJsonStr());
        return true;
    }

    public async Task<bool> CompleteV2Async(string taskCode, BiliCookie ck)
    {
        var request = new ReceiveOrCompleteTaskRequest(taskCode);
        var re = await apiApi.VipBigPointCompleteV2(request, ck.ToString());
        if (re.Code == 0)
        {
            logger.LogInformation("已完成");
            return true;
        }

        logger.LogInformation("失败：{msg}", re.ToJsonStr());
        return false;
    }

    /// <summary>
    /// 观看剧集内容任务：领取 → 开始观看 → 等待倒计时 → 上报完成 → 复查状态
    /// </summary>
    /// <remarks>
    /// 该任务不能只调 complete 接口：服务端要求先 material/receive 拿到 task_id/token，
    /// 等满服务端下发的倒计时（10 分钟）后再带上双重签名上报，否则状态不会推进。
    /// 上报接口常返回 504 等异常，但任务实际可能已完成，因此以 combine 接口的任务状态为准。
    /// </remarks>
    public async Task<bool> OgvWatchAsync(VipBigPointCombine combine, BiliCookie ck)
    {
        const string moduleCode = "日常任务";
        const string taskCode = "ogvwatchnew";

        var taskNow = FindTask(combine, moduleCode, taskCode);
        if (taskNow == null)
        {
            logger.LogInformation("任务失效");
            return false;
        }

        if (taskNow.state == 3)
        {
            logger.LogInformation("已完成，跳过");
            return true;
        }

        if (taskNow.state == 0)
        {
            logger.LogInformation("开始领取任务");
            await TryReceive(taskCode, ck);
        }

        var candidates = await BuildOgvCandidatesAsync(ck);
        if (candidates.Count == 0)
        {
            logger.LogInformation(
                "未取到可观看的剧集，跳过（追番为空，且 VipBigPointConfig:ViewBangumis 未配置）"
            );
            return false;
        }

        // 某一部可能因为地区限制、预告片、专辑未过审等原因播不了，换下一部重试
        BiliApiResponse<StartOgvWatchResponse>? start = null;
        OgvCandidate chosen = default;
        foreach (var candidate in candidates.Take(MaxOgvCandidates))
        {
            var tryStart = await apiApi.StartOgvWatchAsync(
                new StartOgvWatchRequest(candidate.EpId, candidate.SeasonId, ck.BiliJct, ck.Buvid),
                ck.ToString()
            );
            var tryCfg = tryStart.Data?.watch_count_down_cfg;
            if (
                tryStart.Code == 0
                && tryCfg is not null
                && !string.IsNullOrEmpty(tryCfg.task_id)
                && !string.IsNullOrEmpty(tryCfg.token)
            )
            {
                start = tryStart;
                chosen = candidate;
                break;
            }

            logger.LogInformation(
                "这部看不了，换一部：{label}｜{msg}",
                candidate.Label,
                tryStart.ToJsonStr()
            );
        }

        if (start is null)
        {
            logger.LogInformation("开始观看失败：候选剧集都不可用");
            return false;
        }

        var cfg = start.Data!.watch_count_down_cfg!;
        logger.LogInformation("本次播放的正片为：{label}（ep_id={ep}）", chosen.Label, chosen.EpId);

        int waitSeconds =
            cfg.milliseconds > 0
                ? (int)Math.Ceiling(cfg.milliseconds / 1000d) + 10
                : _vipBigPointOptions.OgvWatchWaitSeconds;
        logger.LogInformation(
            "已开始观看：ep_id={ep}，task_id={id}，等待 {sec} 秒后上报",
            chosen.EpId,
            cfg.task_id,
            waitSeconds
        );

        if (waitSeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(waitSeconds));
        }

        string? error = null;
        try
        {
            var complete = await apiApi.CompleteOgvWatchAsync(
                new CompleteOgvWatchRequest(cfg.task_id, cfg.token),
                ck.ToString()
            );
            logger.LogInformation(
                "上报完成返回：code={code} {message}",
                complete.Code,
                complete.Message
            );
        }
        catch (Exception e)
        {
            // B 站该接口常见 504/-400，但服务端可能已记账，不能据此判定失败
            error = e.Message;
            logger.LogInformation("上报完成返回异常（不一定是失败）：{msg}", e.Message);
        }

        var after = await GetCombineAsync(ck);
        var taskAfter = FindTask(after, moduleCode, taskCode);
        bool success = taskAfter is { state: 3, complete_times: >= 1 };
        logger.LogInformation("确认：{re}", success ? "成功，大积分 +10" : "失败");
        if (!success && error != null)
        {
            logger.LogInformation("失败信息：{msg}", error);
        }

        return success;
    }

    #region private

    private static CommonTaskItem? FindTask(
        VipBigPointCombine combine,
        string moduleCode,
        string taskCode
    ) =>
        combine
            .Task_info.Modules.FirstOrDefault(x => x.module_title == moduleCode)
            ?.common_task_item.FirstOrDefault(x => x.task_code == taskCode);

    /// <summary>最多尝试几部剧集：每部失败只多一次请求，不会额外消耗等待时间</summary>
    private const int MaxOgvCandidates = 6;

    /// <summary>候选剧集：ep_id + 所属季 season_id + 日志说明</summary>
    private readonly record struct OgvCandidate(long EpId, long SeasonId, string Label);

    /// <summary>
    /// 挑选可用于「观看剧集内容」的目标，返回按随机顺序排好的候选列表。
    /// </summary>
    /// <remarks>
    /// 优先用「我的追番」里看过的番剧：第 1 话必定已解锁，且追番列表单条数据就带了
    /// first_ep / season_id，不需要再拉整季详情（名侦探柯南一季 1200+ 集，1.2MB）。
    /// 追番为空或接口失败时，回退到 VipBigPointConfig:ViewBangumis 配置的 ssid。
    /// </remarks>
    private async Task<List<OgvCandidate>> BuildOgvCandidatesAsync(BiliCookie ck)
    {
        List<OgvCandidate> fromFollow = _vipBigPointOptions.UseFollowedBangumis
            ? await GetFollowedOgvCandidatesAsync(ck)
            : [];
        if (fromFollow.Count > 0)
            return fromFollow;

        var fromConfig = new List<OgvCandidate>();
        foreach (var ssid in _vipBigPointOptions.ViewBangumiList.OrderBy(_ => Random.Shared.Next()))
        {
            var picked = await GetBangumi(ssid, ck);
            if (picked is not null)
                fromConfig.Add(
                    new OgvCandidate(picked.Value.Item2, ssid, picked.Value.Item1.Title)
                );
        }

        return fromConfig;
    }

    /// <summary>
    /// 从「我的追番」里随机挑几部看过的番剧，每部给出第 1 话（主选）和最新话（备选）。
    /// </summary>
    private async Task<List<OgvCandidate>> GetFollowedOgvCandidatesAsync(BiliCookie ck)
    {
        var result = new List<OgvCandidate>();
        if (!long.TryParse(ck.UserId, out long mid) || mid <= 0)
        {
            logger.LogWarning("Cookie 里没有 DedeUserID，无法读取追番列表，改用配置的番剧");
            return result;
        }

        GetFollowedBangumiResponse? data;
        try
        {
            var res = await apiApi.GetFollowedBangumiList(mid, ck.ToString());
            if (res.Code != 0)
            {
                logger.LogWarning(
                    "读取追番列表失败（code={code} {msg}），改用配置的番剧",
                    res.Code,
                    res.Message
                );
                return result;
            }

            data = res.Data;
        }
        catch (Exception e)
        {
            logger.LogWarning("读取追番列表异常，改用配置的番剧：{msg}", e.Message);
            return result;
        }

        var items = (data?.List ?? [])
            .Where(x => x.season_id > 0 && x.first_ep > 0)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        logger.LogInformation("从追番里挑剧集，共 {count} 部可选", items.Count);

        foreach (var item in items)
        {
            // 进度是「看到第N话」时，第 1 话必定已解锁，不存在付费/地区限制导致播不了的情况
            result.Add(new OgvCandidate(item.first_ep, item.season_id, $"{item.title} 第1话"));

            // 最新话兜底：个别老番的 first_ep 指向预告片
            if (item.new_ep is { id: > 0 } newEp && newEp.id != item.first_ep)
            {
                result.Add(
                    new OgvCandidate(
                        newEp.id,
                        item.season_id,
                        $"{item.title} {newEp.index_show ?? newEp.long_title ?? "最新话"}"
                    )
                );
            }
        }

        return result;
    }

    /// <summary>
    /// 领取任务
    /// </summary>
    private async Task TryReceive(string taskCode, BiliCookie ck)
    {
        BiliApiResponse? re = null;
        try
        {
            // 「观看剧集内容」对客户端版本敏感，只传 taskCode + build 会被判为旧客户端
            // （6007000），需要补 App 上下文参数，故单独走一个重载。
            re =
                taskCode == "ogvwatchnew"
                    ? await apiApi.VipBigPointReceiveV2ForOgv(
                        new OgvReceiveRequest { csrf = ck.BiliJct, buvid = ck.Buvid },
                        ck.ToString()
                    )
                    : await apiApi.VipBigPointReceiveV2(
                        new ReceiveOrCompleteTaskRequest(taskCode),
                        ck.ToString()
                    );
            if (re.Code == 0)
                logger.LogInformation("领取任务成功");
            else
                logger.LogInformation("领取任务失败：{msg}", re.ToJsonStr());
        }
        catch (Exception e)
        {
            logger.LogError("领取任务异常");
            logger.LogError(e.Message + re?.ToJsonStr());
        }
    }

    private async Task<bool> WatchBangumi(BiliCookie ck)
    {
        if (_vipBigPointOptions.ViewBangumiList.Count == 0)
            return false;

        long randomSsid = _vipBigPointOptions.ViewBangumiList[
            new Random().Next(0, _vipBigPointOptions.ViewBangumiList.Count)
        ];

        var res = await GetBangumi(randomSsid, ck);
        if (res is null)
        {
            return false;
        }

        var videoInfo = res.Value.Item1;

        // 随机播放时间
        int playedTime = new Random().Next(905, 1800);
        // 观看该视频
        var request = new UploadVideoHeartbeatRequest()
        {
            Aid = long.Parse(videoInfo.Aid),
            Bvid = videoInfo.Bvid,
            Cid = videoInfo.Cid,
            Mid = long.Parse(ck.UserId),
            Sid = randomSsid,
            Epid = res.Value.Item2,
            Csrf = ck.BiliJct,
            Type = 4,
            Sub_type = 1,
            Start_ts = DateTime.Now.ToTimeStamp() - playedTime,
            Played_time = playedTime,
            Realtime = playedTime,
            Real_played_time = playedTime,
        };
        BiliApiResponse apiResponse = await apiApi.UploadVideoHeartbeat(
            request.Aid,
            request.Played_time,
            request,
            ck.ToString()
        );
        if (apiResponse.Code == 0)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 从自定义的番剧ssid中选择其中的一部中的一集
    /// </summary>
    /// <param name="randomSsid">番剧ssid</param>
    /// <returns></returns>
    /// <summary>
    /// 拉取某一季的剧集列表并随机取一集。仅作为「追番列表不可用」时的兜底。
    /// </summary>
    private async Task<(VideoInfoDto, long)?> GetBangumi(long ssid, BiliCookie ck)
    {
        try
        {
            if (ssid is 0 or long.MinValue)
                return null;

            var bangumiInfo = await apiApi.GetBangumiBySsid(ssid, ck.ToString());
            var episodes = bangumiInfo.Result?.episodes;
            if (episodes is null || episodes.Count == 0)
            {
                logger.LogWarning("番剧 {ssid} 没有可用的剧集", ssid);
                return null;
            }

            var episode = episodes[Random.Shared.Next(0, episodes.Count)];
            var videoInfo = new VideoInfoDto
            {
                Bvid = episode.bvid,
                Aid = episode.aid.ToString(),
                Cid = episode.cid,
                Copyright = 1,
                Duration = episode.duration,
                Title = episode.share_copy,
            };
            return (videoInfo, episode.ep_id);
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
        }

        return null;
    }

    #endregion
}
