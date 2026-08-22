using System;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;
using BiliLearn.CSharp.Plugin.Models;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Services;

public class BiliLearnProgressReporter : IProgressReporter
{
    private readonly ILogger _logger;
    private readonly Func<string, Task>? _onProgress;

    public BiliLearnProgressReporter(ILogger logger, Func<string, Task>? onProgress = null)
    {
        _logger = logger;
        _onProgress = onProgress;
    }

    public async Task ReportAsync(string message, ProgressLevel level = ProgressLevel.LogOnly)
    {
        try
        {
            _logger.LogInformation("[BiliLearn] {Message}", message);

            if (level == ProgressLevel.LogAndPush && _onProgress != null)
            {
                await _onProgress(message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BiliLearn] 进度报告异常");
        }
    }
}
