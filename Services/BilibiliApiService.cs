using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;
using BiliLearn.CSharp.Plugin.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace BiliLearn.CSharp.Plugin.Services;

/// <summary>
/// B站 API 服务：登录验证、视频信息、音视频流地址获取
/// </summary>
public class BilibiliApiService : IBilibiliFetcher, IDisposable
{
    private const string BaseUrl = "https://api.bilibili.com";
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private bool _disposed = false;

    public BilibiliApiService(string cookieString, ILogger logger)
    {
        _logger = logger;
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new System.Net.CookieContainer()
        };
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        if (!string.IsNullOrEmpty(cookieString))
        {
            foreach (var part in cookieString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = part.IndexOf('=');
                if (idx > 0)
                {
                    var name = part[..idx].Trim();
                    var value = part[(idx + 1)..].Trim();
                    handler.CookieContainer.Add(new System.Net.Cookie(name, value, "/", ".bilibili.com"));
                }
            }
        }
    }

    public async Task<LoginStatus> VerifyLoginAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetStringAsync($"{BaseUrl}/x/web-interface/nav", ct);
            var data = JObject.Parse(response);
            
            // 防御性检查：code 字段是否存在
            var codeToken = data["code"];
            if (codeToken == null)
            {
                _logger.LogWarning("登录验证响应缺少 code 字段，原始响应: {Response}", response);
                return new LoginStatus { Valid = false, Message = "响应格式异常，缺少 code 字段" };
            }
            
            var code = codeToken.Value<int>();
            if (code == 0)
            {
                var userData = data["data"];
                if (userData == null)
                {
                    _logger.LogWarning("登录验证响应缺少 data 字段");
                    return new LoginStatus { Valid = false, Message = "响应格式异常，缺少 data 字段" };
                }
                
                var midToken = userData["mid"];
                var unameToken = userData["uname"];
                if (midToken == null || unameToken == null)
                {
                    _logger.LogWarning("登录验证响应缺少 mid 或 uname 字段");
                    return new LoginStatus { Valid = false, Message = "用户信息不完整" };
                }
                
                var vipStatus = userData["vipStatus"]?.Value<int>() ?? 0;
                var vipLabel = vipStatus == 1 ? "大会员" : "";
                
                // 同时设置新旧属性，兼容 Orchestrator
                return new LoginStatus
                {
                    // 新属性（接口定义）
                    IsLogin = true,
                    Mid = midToken.Value<long>(),
                    Uname = unameToken.Value<string>(),
                    IsVip = vipStatus == 1,
                    VipLabel = vipLabel,
                    // 旧属性（Orchestrator 使用）
                    Valid = true,
                    Uid = midToken.Value<string>(),
                    UserName = unameToken.Value<string>(),
                    Message = "登录有效"
                };
            }
            else
            {
                var message = data["message"]?.Value<string>() ?? "登录验证失败";
                return new LoginStatus { Valid = false, Message = message };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "登录验证异常");
            return new LoginStatus { Valid = false, Message = $"异常: {ex.Message}" };
        }
    }

    public async Task<VideoInfoResult> GetVideoInfoAsync(string bvid, CancellationToken ct = default)
    {
        try
        {
            // 获取基本信息
            var response = await _httpClient.GetStringAsync($"{BaseUrl}/x/web-interface/view?bvid={bvid}", ct);
            var data = JObject.Parse(response);
            
            if (data["code"]?.Value<int>() != 0)
            {
                return new VideoInfoResult { Success = false, Message = data["message"]?.Value<string>() ?? "未知错误" };
            }
            
            var videoData = data["data"];
            var result = new VideoInfoResult
            {
                Success = true,
                Data = new VideoInfo
                {
                    Bvid = bvid,
                    Cid = videoData["cid"]?.Value<long>() ?? 0,
                    Title = videoData["title"]?.Value<string>() ?? "",
                    DurationSeconds = videoData["duration"]?.Value<int>() ?? 0,
                    Description = videoData["description"]?.Value<string>(),
                    Pic = videoData["pic"]?.Value<string>(),
                    Owner = videoData["owner"]?["name"]?.Value<string>(),
                    IsExclusiveForVip = videoData["reserve"]?.Value<bool>() ?? false,
                    NeedCharge = videoData["pay"]?.Value<int>() == 1,
                    NeedVip = videoData["vipPrivilege"]?.Value<bool>() ?? false
                }
            };
            
            // 获取播放地址 - 需要单独调用playurl接口
            var cid = result.Data.Cid;
            var playResp = await _httpClient.GetStringAsync(
                $"{BaseUrl}/x/player/playurl?bvid={bvid}&cid={cid}&fnval=16&qn=0&fourk=1", ct);
            var playJson = JObject.Parse(playResp);
            
            if (playJson["code"]?.Value<int>() == 0)
            {
                var dash = playJson["data"]?["dash"];
                if (dash != null)
                {
                    // 音频：选带宽最高的
                    var audioArr = dash["audio"] as JArray;
                    if (audioArr != null && audioArr.Count > 0)
                    {
                        JToken? best = null;
                        long bestBw = -1;
                        foreach (var a in audioArr)
                        {
                            var bw = a["bandwidth"]?.Value<long>() ?? 0;
                            if (bw > bestBw) { bestBw = bw; best = a; }
                        }
                        if (best != null)
                        {
                            result.Data.AudioUrl = best["baseUrl"]?.Value<string>() ?? best["base_url"]?.Value<string>() ?? "";
                            _logger.LogInformation("音频URL: {Url}", result.Data.AudioUrl);
                        }
                    }

                    // 视频：选带宽最低的（够画面分析用）
                    var videoArr = dash["video"] as JArray;
                    if (videoArr != null && videoArr.Count > 0)
                    {
                        JToken? lowest = null;
                        long lowestBw = long.MaxValue;
                        foreach (var v in videoArr)
                        {
                            var bw = v["bandwidth"]?.Value<long>() ?? 0;
                            if (bw < lowestBw) { lowestBw = bw; lowest = v; }
                        }
                        if (lowest != null)
                        {
                            result.Data.VideoUrl = lowest["baseUrl"]?.Value<string>() ?? lowest["base_url"]?.Value<string>() ?? "";
                            _logger.LogInformation("视频URL: {Url}", result.Data.VideoUrl);
                        }
                    }
                }
                else
                {
                    // 非DASH模式（durl）
                    var durl = playJson["data"]?["durl"] as JArray;
                    if (durl != null && durl.Count > 0)
                    {
                        result.Data.VideoUrl = durl[0]?["url"]?.Value<string>() ?? "";
                        result.Data.AudioUrl = durl[0]?["url"]?.Value<string>() ?? "";
                    }
                }
            }
            else
            {
                _logger.LogWarning("获取播放地址失败: {Bvid} {Cid}, 响应: {Response}", bvid, cid, playResp);
            }
            
            // 解析标签
            var tagList = videoData["tag_list"] as JArray;
            if (tagList != null)
            {
                result.Data.Tags = tagList.Select(t => t["tag_name"]?.Value<string>()).Where(t => t != null).ToList() ?? new List<string>();
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取视频信息失败: {Bvid}", bvid);
            return new VideoInfoResult { Success = false, Message = ex.Message };
        }
    }

    public async Task<string?> GetSubtitleAsync(string bvid, long cid, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetStringAsync($"{BaseUrl}/x/player/wbi/v2?bvid={bvid}&cid={cid}", ct);
            var data = JObject.Parse(response);
            
            if (data["code"]?.Value<int>() != 0)
            {
                _logger.LogWarning("获取字幕失败: {Bvid} {Cid}", bvid, cid);
                return null;
            }
            
            var subtitleData = data["data"]?["subtitle"];
            if (subtitleData == null || !subtitleData.HasValues)
            {
                _logger.LogInformation("视频 {Bvid} 没有字幕", bvid);
                return null;
            }
            
            var subtitleUrl = subtitleData["subtitles"]?.FirstOrDefault()?["subtitle_url"]?.Value<string>();
            if (string.IsNullOrEmpty(subtitleUrl))
            {
                return null;
            }
            
            // 下载字幕文件
            var subtitleJson = await _httpClient.GetStringAsync($"https:{subtitleUrl}", ct);
            _logger.LogInformation("成功获取字幕: {Bvid}", bvid);
            return subtitleJson;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取字幕失败: {Bvid} {Cid}", bvid, cid);
            return null;
        }
    }

    public async Task<List<RecommendItem>> GetRecommendAsync(int count = 10, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetStringAsync($"{BaseUrl}/x/web-interface/popular/white?ps={count}&pn=1", ct);
            var data = JObject.Parse(response);
            
            if (data["code"]?.Value<int>() != 0)
            {
                return new List<RecommendItem>();
            }
            
            return data["data"]?["list"]?.Select(item => new RecommendItem
            {
                Bvid = item["bvid"]?.Value<string>() ?? "",
                Title = item["title"]?.Value<string>() ?? "",
                Author = item["author"]?.Value<string>() ?? "",
                Duration = item["duration"]?.Value<string>() ?? "",
                Pic = item["pic"]?.Value<string>() ?? ""
            }).ToList() ?? new List<RecommendItem>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取推荐视频失败");
            return new List<RecommendItem>();
        }
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}
