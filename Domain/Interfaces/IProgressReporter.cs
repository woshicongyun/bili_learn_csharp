using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Models;

namespace BiliLearn.CSharp.Plugin.Domain.Interfaces;

public interface IProgressReporter
{
    Task ReportAsync(string message, ProgressLevel level = ProgressLevel.LogOnly);
}
