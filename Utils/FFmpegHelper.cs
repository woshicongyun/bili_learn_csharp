
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Utils;

/// <summary>
/// FFmpeg 工具类：查找路径、抽取关键帧
/// </summary>
public static class FFmpegHelper
{
    public static string FindPath()
    {
        string[] candidates = {
            "ffmpeg",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Python", "Python313", "Lib", "site-packages", "imageio_ffmpeg", "binaries", "ffmpeg-win-x86_64-v7.1.exe"),
            @"C:\ffmpeg\bin\ffmpeg.exe",
            @"D:\ffmpeg\bin\ffmpeg.exe"
        };

        foreach (var c in candidates)
        {
            try
            {
                if (c == "ffmpeg")
                {
                    using var p = Process.Start(new ProcessStartInfo("ffmpeg", "-version")
                    { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
                    if (p != null) { p.WaitForExit(3000); return "ffmpeg"; }
                }
                else if (File.Exists(c)) return c;
            }
            catch { }
        }
        return "ffmpeg"; // 让系统尝试，如果失败会抛异常
    }

    /// <summary>
    /// 从视频抽取关键帧（每interval秒抽一帧，最多maxFrames张）
    /// </summary>
    public static async Task<List<string>> ExtractFramesAsync(string videoPath, string workDir, int durationSec, int interval, int maxFrames, ILogger logger, CancellationToken cancellationToken = default)
    {
        var frames = new List<string>();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = FindPath(),
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        for (int t = 0, count = 0; t < durationSec && count < maxFrames; t += interval, count++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var framePath = Path.Combine(workDir, $"frame_{count:D3}.jpg");
            process.StartInfo.ArgumentList.Clear();
            process.StartInfo.ArgumentList.Add("-ss");
            process.StartInfo.ArgumentList.Add(t.ToString());
            process.StartInfo.ArgumentList.Add("-i");
            process.StartInfo.ArgumentList.Add(videoPath);
            process.StartInfo.ArgumentList.Add("-frames:v");
            process.StartInfo.ArgumentList.Add("1");
            process.StartInfo.ArgumentList.Add("-q:v");
            process.StartInfo.ArgumentList.Add("2");
            process.StartInfo.ArgumentList.Add(framePath);
            process.StartInfo.ArgumentList.Add("-y");

            try
            {
                process.Start();
                await process.WaitForExitAsync(cancellationToken);
                if (File.Exists(framePath) && new FileInfo(framePath).Length > 0)
                    frames.Add(framePath);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                logger.LogInformation("抽取关键帧已取消: t={Time}s", t);
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "抽取关键帧失败: t={Time}s", t);
            }
        }

        logger.LogInformation("关键帧提取: {Count}帧", frames.Count);
        return frames;
    }
}
