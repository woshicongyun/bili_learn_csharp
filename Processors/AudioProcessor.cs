using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Alife.Function.AIModelUtility;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;
using BiliLearn.CSharp.Plugin.Models;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Processors;

/// <summary>
/// 音频处理器：调用Alife SenseVoice进行ASR转写
/// </summary>
public class AudioProcessor : IMediaAnalyzer
{
    private static readonly Type? _audioDecoderType = Type.GetType("Alife.Function.Auditory.AudioRecognitionService.AudioDecoder, Alife.Function.Auditory");
    private readonly IAudioRecognizerProvider? _provider;
    private readonly ILogger _logger;

    public AudioProcessor(IAudioRecognizerProvider? provider, ILogger logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public bool Available => _provider != null;

    public async Task<string?> TranscribeAsync(string audioPath, CancellationToken ct = default)
    {
        if (_provider == null)
        {
            _logger.LogWarning("SenseVoice未注入，无法进行ASR");
            return null;
        }
        try
        {
            ct.ThrowIfCancellationRequested();
            _logger.LogInformation("🎙 开始ASR转写: {Path}", audioPath);
            var samples = await Task.Run(() => DecodeTo16kMonoFloat(audioPath, ct), ct);
            if (samples == null || samples.Length == 0)
            {
                _logger.LogWarning("音频解码失败");
                return null;
            }
            _logger.LogInformation("音频解码完成: {Samples}采样 ({Sec:N1}秒)", samples.Length, samples.Length / 16000.0);

            using var recognizer = _provider.CreateAudioRecognizer();
            var recognizedTexts = new List<string>();
            void OnRecognized(string text)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    recognizedTexts.Add(text);
                    _logger.LogInformation("  ASR: {Text}", text);
                }
            }
            recognizer.Recognized += OnRecognized;
            try
            {
                recognizer.AcceptWaveform(samples, samples.Length);
                recognizer.Flush();
            }
            finally
            {
                recognizer.Recognized -= OnRecognized;
            }
            var result = string.Join(" ", recognizedTexts);
            _logger.LogInformation("✅ ASR转写完成: {Len}字", result.Length);
            return result.Length > 0 ? result : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ASR转写失败");
            return null;
        }
    }

    public Task<List<FrameDescription>> AnalyzeVisualAsync(string videoPath, string workDir, int durationSeconds, int intervalSeconds, int maxFrames, ILogger logger, CancellationToken ct = default)
        => Task.FromResult(new List<FrameDescription>());

    public Task<List<StructuredSubtitle>> ParseSubtitleAsync(string subtitleJson)
        => Task.FromResult(new List<StructuredSubtitle>());

    private float[] DecodeTo16kMonoFloat(string path, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            if (_audioDecoderType == null)
            {
                _logger.LogWarning("AudioDecoder类型不可用，请确认Auditory插件已加载");
                return Array.Empty<float>();
            }
            var method = _audioDecoderType.GetMethod("DecodeFileTo16kMonoFloat", new[] { typeof(string) });
            if (method == null)
            {
                _logger.LogWarning("AudioDecoder.DecodeFileTo16kMonoFloat方法不可用");
                return Array.Empty<float>();
            }
            var result = method.Invoke(null, new object[] { path });
            return result as float[] ?? Array.Empty<float>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "音频解码失败: {Path}", path);
            return Array.Empty<float>();
        }
    }

    public void Dispose() { }
}
