using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            // 反射调用Alife内置AudioDecoder（避免引用NAudio导致全局冲突）
            var audioDecoderType = Type.GetType("Alife.Function.Auditory.AudioDecoder, Alife.Function.Auditory");
            if (audioDecoderType == null)
            {
                _logger.LogWarning("未找到Alife.AudioDecoder类型，尝试其他程序集");
                audioDecoderType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.Name == "AudioDecoder" && t.Namespace != null && t.Namespace.Contains("Auditory"));
            }

            if (audioDecoderType == null)
            {
                _logger.LogWarning("Alife.AudioDecoder类型不存在，无法解码音频");
                return Array.Empty<float>();
            }

            var method = audioDecoderType.GetMethod("DecodeFileTo16kMonoFloat",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);

            if (method == null)
            {
                _logger.LogWarning("Alife.AudioDecoder.DecodeFileTo16kMonoFloat方法不存在");
                return Array.Empty<float>();
            }

            var result = method.Invoke(null, new object[] { path });
            if (result is float[] samples)
            {
                _logger.LogInformation("✅ Alife解码: {Samples}采样 ({Sec:N1}秒)", samples.Length, samples.Length / 16000.0);
                return samples;
            }

            _logger.LogWarning("Alife解码返回类型异常");
            return Array.Empty<float>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "音频解码失败(反射Alife.AudioDecoder): {Path}", path);
            return Array.Empty<float>();
        }
    }

    public void Dispose() { }
}
