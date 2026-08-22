
using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Utils;

/// <summary>
/// 二维码生成工具（用于B站扫码登录）
/// 通过调用Python的qrcode库生成PNG图片，避免引入额外NuGet依赖
/// </summary>
public static class QrCodeGenerator
{
    /// <summary>
    /// 生成二维码PNG，返回图片文件路径
    /// </summary>
    public static string GeneratePng(string text, string saveDir, ILogger? logger = null)
    {
        try
        {
            Directory.CreateDirectory(saveDir);
            var fileName = $"qr_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var filePath = Path.Combine(saveDir, fileName);

            // 调用Python脚本生成二维码
            var script = $@"
import qrcode, sys, os
qr = qrcode.QRCode(version=None, error_correction=qrcode.constants.ERROR_CORRECT_M, box_size=8, border=4)
qr.add_data(sys.argv[1])
qr.make(fit=True)
img = qr.make_image(fill_color='black', back_color='white')
img.save(sys.argv[2])
print(f'OK {{os.path.getsize(sys.argv[2])}}')
";
            var scriptPath = Path.Combine(Path.GetTempPath(), $"qr_{Guid.NewGuid():N}.py");
            File.WriteAllText(scriptPath, script);
            var scriptArg = scriptPath.Replace("\"", "\\\"");

            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{scriptArg}\" \"{text.Replace("\\", "\\\\").Replace("\"", "\\\"")}\" \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                logger?.LogWarning("无法启动Python进程生成二维码");
                return "";
            }
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(10000);
            logger?.LogInformation("Python二维码输出: {Stdout} 错误: {Stderr}", stdout, stderr);

            // 清理临时Python脚本
            if (File.Exists(scriptPath))
            {
                try { File.Delete(scriptPath); } catch { }
            }

            if (process.ExitCode != 0 || !File.Exists(filePath))
            {
                logger?.LogWarning("Python生成二维码失败: {Stdout} {Stderr}", stdout, stderr);
                return "";
            }

            var size = new FileInfo(filePath).Length;
            logger?.LogInformation("二维码图片已生成: {Path} ({Size} bytes)", filePath, size);
            return filePath;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "生成二维码图片失败");
            return "";
        }
    }
}
