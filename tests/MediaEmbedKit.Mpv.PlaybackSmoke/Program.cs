using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv.Platforms;
using MediaEmbedKit.Mpv.Externals;
using MediaEmbedKit.Mpv.Runtime;
using MediaEmbedKit.Mpv.Diagnostics;

namespace MediaEmbedKit.Mpv.PlaybackSmoke;

/// <summary>
/// 執行範例應用程式播放冒煙測試。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 測試執行進入點。
    /// </summary>
    /// <param name="args">
    /// 命令列引數。
    /// </param>
    /// <returns>
    /// 所有指定範例通過時傳回 0，否則傳回 1。
    /// </returns>
    private static async Task<int> Main(string[] args)
    {
        ConfigureConsoleEncoding();
        SmokeOptions options = SmokeOptions.Parse(args);
        IReadOnlyList<SmokeSample> samples = SmokeSampleCatalog.GetSamples(options.SampleName);
        if (samples.Count == 0)
        {
            Console.Error.WriteLine("找不到指定的範例：" + options.SampleName);
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(options.RuntimeDirectory))
        {
            await RuntimeProvisioner.EnsureAsync(options.RuntimeDirectory!).ConfigureAwait(false);
        }

        int failedCount = 0;
        for (int iteration = 1; iteration <= options.Iterations; iteration++)
        {
            foreach (SmokeSample sample in samples)
            {
                int exitCode = await RunSampleAsync(sample, options, iteration).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    failedCount++;
                }
            }
        }

        int totalCount = samples.Count * options.Iterations;
        Console.WriteLine("播放冒煙測試完成：通過 " + (totalCount - failedCount).ToString(CultureInfo.InvariantCulture) + "，失敗 " + failedCount.ToString(CultureInfo.InvariantCulture) + "。");
        return failedCount == 0 ? 0 : 1;
    }

    /// <summary>
    /// 將測試輸出固定為 UTF-8，避免 Windows CI 將中文測試名稱轉成問號。
    /// </summary>
    private static void ConfigureConsoleEncoding()
    {
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }

    /// <summary>
    /// 執行單一範例應用程式。
    /// </summary>
    /// <param name="sample">
    /// 要執行的範例。
    /// </param>
    /// <param name="options">
    /// 冒煙測試選項。
    /// </param>
    /// <param name="iteration">
    /// 目前重複執行次數。
    /// </param>
    /// <returns>
    /// 範例處理序結束代碼。
    /// </returns>
    private static async Task<int> RunSampleAsync(SmokeSample sample, SmokeOptions options, int iteration)
    {
        string projectPath = sample.ResolveProjectPath(options.SampleRoot);
        Console.WriteLine("[smoke] 開始 " + sample.Name + " 第 " + iteration.ToString(CultureInfo.InvariantCulture) + " 次，至少播放 " + options.Seconds.ToString("0.###", CultureInfo.InvariantCulture) + " 秒。");
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        sample.AddRunArguments(startInfo.ArgumentList, options, projectPath);
        startInfo.Environment["MEDIAEMBEDKIT_MPV_SAMPLE_SMOKE"] = "1";
        startInfo.Environment["MEDIAEMBEDKIT_MPV_SAMPLE_SMOKE_SECONDS"] = options.Seconds.ToString("0.###", CultureInfo.InvariantCulture);
        if (options.FeatureSmoke)
        {
            startInfo.Environment["MEDIAEMBEDKIT_MPV_SAMPLE_FEATURE_SMOKE"] = "1";
        }

        if (!string.IsNullOrWhiteSpace(options.RuntimeDirectory))
        {
            startInfo.Environment["MEDIAEMBEDKIT_MPV_RUNTIME_DIR"] = Path.GetFullPath(options.RuntimeDirectory!);
        }

        using (Process process = new Process())
        using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds)))
        {
            process.StartInfo = startInfo;
            process.OutputDataReceived += delegate (object sender, DataReceivedEventArgs e)
            {
                WriteProcessLine(sample.Name, "out", e.Data);
            };
            process.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs e)
            {
                WriteProcessLine(sample.Name, "err", e.Data);
            };

            if (!process.Start())
            {
                Console.Error.WriteLine("[smoke] " + sample.Name + " 無法啟動。");
                return 1;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                Console.Error.WriteLine("[smoke] " + sample.Name + " 等待逾時。");
                return 1;
            }

            Console.WriteLine("[smoke] " + sample.Name + " exit=" + process.ExitCode.ToString(CultureInfo.InvariantCulture));
            return process.ExitCode;
        }
    }

    /// <summary>
    /// 輸出範例處理序的一列文字。
    /// </summary>
    /// <param name="sampleName">
    /// 範例名稱。
    /// </param>
    /// <param name="streamName">
    /// 輸出資料流名稱。
    /// </param>
    /// <param name="line">
    /// 輸出文字。
    /// </param>
    private static void WriteProcessLine(string sampleName, string streamName, string? line)
    {
        if (line == null)
        {
            return;
        }

        Console.WriteLine("[" + sampleName + ":" + streamName + "] " + line);
    }

    /// <summary>
    /// 嘗試終止處理序與其子處理序。
    /// </summary>
    /// <param name="process">
    /// 要終止的處理序。
    /// </param>
    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}

/// <summary>
/// 在啟動 GUI sample 前準備共用執行階段資料夾。
/// </summary>
internal static class RuntimeProvisioner
{
    /// <summary>
    /// 下載失敗後的最大重試次數。
    /// </summary>
    private const int RetryCount = 3;

    /// <summary>
    /// 確認指定執行階段資料夾包含播放需要的執行階段檔案。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 要檢查或建立的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表準備流程的工作。
    /// </returns>
    public static async Task EnsureAsync(string runtimeDirectory)
    {
        string fullPath = Path.GetFullPath(runtimeDirectory);
        if (HasCompleteRuntime(fullPath))
        {
            Console.WriteLine("[smoke] 共用 runtime 已存在：" + fullPath);
            return;
        }

        MpvRuntimeInstallOptions options = new MpvRuntimeInstallOptions();
        options.Windows.LoadLibMpv = false;
        options.Windows.IncludeFFmpeg = true;

        for (int attempt = 1; attempt <= RetryCount; attempt++)
        {
            try
            {
                Console.WriteLine("[smoke] 準備共用 runtime，第 " + attempt.ToString(CultureInfo.InvariantCulture) + " 次：" + fullPath);
                await MpvRuntimeInstaller.InstallOrUpdateAsync(fullPath, options).ConfigureAwait(false);
                if (!HasCompleteRuntime(fullPath))
                {
                    throw new InvalidOperationException("執行階段資料夾未包含 libmpv-2.dll、yt-dlp.exe、deno.exe、ffmpeg.exe 與 ffprobe.exe。");
                }

                Console.WriteLine("[smoke] 共用執行階段準備完成：" + fullPath);
                return;
            }
            catch (Exception ex) when (attempt < RetryCount)
            {
                Console.Error.WriteLine("[smoke]執行階段準備失敗，將重試：" + ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt)).ConfigureAwait(false);
            }
        }

        Console.WriteLine("[smoke] 準備共用 runtime，最後一次：" + fullPath);
        await MpvRuntimeInstaller.InstallOrUpdateAsync(fullPath, options).ConfigureAwait(false);
        if (!HasCompleteRuntime(fullPath))
        {
            throw new InvalidOperationException("執行階段資料夾未包含 libmpv-2.dll、yt-dlp.exe、deno.exe、ffmpeg.exe 與 ffprobe.exe。");
        }
    }

    /// <summary>
    /// 判斷指定資料夾是否包含播放測試需要的檔案。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 要檢查的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 資料夾包含必要檔案時為 <see langword="true"/>。
    /// </returns>
    private static bool HasCompleteRuntime(string runtimeDirectory)
    {
        return File.Exists(Path.Combine(runtimeDirectory, "libmpv-2.dll"))
            && File.Exists(Path.Combine(runtimeDirectory, "yt-dlp.exe"))
            && File.Exists(Path.Combine(runtimeDirectory, "deno.exe"))
            && File.Exists(Path.Combine(runtimeDirectory, "ffmpeg.exe"))
            && File.Exists(Path.Combine(runtimeDirectory, "ffprobe.exe"));
    }
}

/// <summary>
/// 表示播放冒煙測試命令列選項。
/// </summary>
internal sealed class SmokeOptions
{
    /// <summary>
    /// 初始化 <see cref="SmokeOptions"/> 類別的新執行個體。
    /// </summary>
    public SmokeOptions()
    {
        Seconds = 20;
        SampleName = "all";
        SampleRoot = ".";
        Configuration = "Debug";
        TimeoutSeconds = 240;
        Iterations = 1;
    }

    /// <summary>
    /// 取得或設定必須播放的最少秒數。
    /// </summary>
    /// <value>
    /// 必須播放的最少秒數。
    /// </value>
    public double Seconds { get; set; }

    /// <summary>
    /// 取得或設定要執行的範例名稱。
    /// </summary>
    /// <value>
    /// 範例名稱或 <c>all</c>。
    /// </value>
    public string SampleName { get; set; }

    /// <summary>
    /// 取得或設定範例專案路徑的根目錄。
    /// </summary>
    /// <value>
    /// 包含 `samples` 資料夾的根目錄。
    /// </value>
    public string SampleRoot { get; set; }

    /// <summary>
    /// 取得或設定建置組態。
    /// </summary>
    /// <value>
    /// 建置組態。
    /// </value>
    public string Configuration { get; set; }

    /// <summary>
    /// 取得或設定是否略過建置。
    /// </summary>
    /// <value>
    /// 略過建置時為 <see langword="true"/>。
    /// </value>
    public bool NoBuild { get; set; }

    /// <summary>
    /// 取得或設定範例處理序使用的共用執行階段資料夾。
    /// </summary>
    /// <value>
    /// 共用執行階段資料夾；未指定時由範例自行決定。
    /// </value>
    public string? RuntimeDirectory { get; set; }

    /// <summary>
    /// 取得或設定單一範例的等待逾時秒數。
    /// </summary>
    /// <value>
    /// 單一範例的等待逾時秒數。
    /// </value>
    public int TimeoutSeconds { get; set; }

    /// <summary>
    /// 取得或設定每個範例要重複執行的次數。
    /// </summary>
    /// <value>
    /// 每個範例的重複執行次數。
    /// </value>
    public int Iterations { get; set; }

    /// <summary>
    /// 取得或設定是否啟用範例功能冒煙測試。
    /// </summary>
    /// <value>
    /// 啟用範例功能冒煙測試時為 <see langword="true"/>。
    /// </value>
    public bool FeatureSmoke { get; set; }

    /// <summary>
    /// 解析命令列引數。
    /// </summary>
    /// <param name="args">
    /// 命令列引數。
    /// </param>
    /// <returns>
    /// 解析後的選項。
    /// </returns>
    public static SmokeOptions Parse(string[] args)
    {
        SmokeOptions options = new SmokeOptions();
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            switch (argument)
            {
                case "--seconds":
                    options.Seconds = ParseDouble(ReadValue(args, ref index, argument), argument);
                    break;
                case "--sample":
                    options.SampleName = ReadValue(args, ref index, argument);
                    break;
                case "--sample-root":
                    options.SampleRoot = ReadValue(args, ref index, argument);
                    break;
                case "--configuration":
                    options.Configuration = ReadValue(args, ref index, argument);
                    break;
                case "--timeout-seconds":
                    options.TimeoutSeconds = ParseInt(ReadValue(args, ref index, argument), argument);
                    break;
                case "--runtime-directory":
                    options.RuntimeDirectory = ReadValue(args, ref index, argument);
                    break;
                case "--iterations":
                    options.Iterations = ParseInt(ReadValue(args, ref index, argument), argument);
                    break;
                case "--feature-smoke":
                    options.FeatureSmoke = true;
                    break;
                case "--no-build":
                    options.NoBuild = true;
                    break;
                default:
                    throw new ArgumentException("不支援的引數：" + argument);
            }
        }

        if (options.Seconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Seconds), "播放秒數必須大於零。");
        }

        if (options.TimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TimeoutSeconds), "逾時秒數必須大於零。");
        }

        if (options.Iterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Iterations), "重複次數必須大於零。");
        }

        if (string.IsNullOrWhiteSpace(options.SampleRoot))
        {
            throw new ArgumentException("範例根目錄不可為空白。", nameof(SampleRoot));
        }

        return options;
    }

    /// <summary>
    /// 讀取指定引數後方的值。
    /// </summary>
    /// <param name="args">
    /// 命令列引數。
    /// </param>
    /// <param name="index">
    /// 目前引數索引。
    /// </param>
    /// <param name="name">
    /// 引數名稱。
    /// </param>
    /// <returns>
    /// 引數值。
    /// </returns>
    private static string ReadValue(string[] args, ref int index, string name)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException(name + " 需要值。");
        }

        index++;
        return args[index];
    }

    /// <summary>
    /// 將文字解析為浮點數。
    /// </summary>
    /// <param name="value">
    /// 要解析的文字。
    /// </param>
    /// <param name="name">
    /// 引數名稱。
    /// </param>
    /// <returns>
    /// 解析後的浮點數。
    /// </returns>
    private static double ParseDouble(string value, string name)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
        {
            throw new ArgumentException(name + " 必須是數字。");
        }

        return result;
    }

    /// <summary>
    /// 將文字解析為整數。
    /// </summary>
    /// <param name="value">
    /// 要解析的文字。
    /// </param>
    /// <param name="name">
    /// 引數名稱。
    /// </param>
    /// <returns>
    /// 解析後的整數。
    /// </returns>
    private static int ParseInt(string value, string name)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
        {
            throw new ArgumentException(name + " 必須是整數。");
        }

        return result;
    }
}

/// <summary>
/// 表示一個可執行的範例應用程式。
/// </summary>
internal sealed class SmokeSample
{
    /// <summary>
    /// 初始化 <see cref="SmokeSample"/> 類別的新執行個體。
    /// </summary>
    /// <param name="name">
    /// 範例名稱。
    /// </param>
    /// <param name="projectPath">
    /// 範例專案路徑。
    /// </param>
    /// <param name="framework">
    /// 目標框架。
    /// </param>
    /// <param name="runtimeIdentifier">
    /// 執行階段識別項。
    /// </param>
    public SmokeSample(string name, string projectPath, string framework, string? runtimeIdentifier)
    {
        Name = name;
        ProjectPath = projectPath;
        Framework = framework;
        RuntimeIdentifier = runtimeIdentifier;
    }

    /// <summary>
    /// 取得範例名稱。
    /// </summary>
    /// <value>
    /// 範例名稱。
    /// </value>
    public string Name { get; private set; }

    /// <summary>
    /// 取得範例專案路徑。
    /// </summary>
    /// <value>
    /// 範例專案路徑。
    /// </value>
    public string ProjectPath { get; private set; }

    /// <summary>
    /// 取得目標框架。
    /// </summary>
    /// <value>
    /// 目標框架。
    /// </value>
    public string Framework { get; private set; }

    /// <summary>
    /// 取得執行階段識別項。
    /// </summary>
    /// <value>
    /// 執行階段識別項；不需要時為 <see langword="null"/>。
    /// </value>
    public string? RuntimeIdentifier { get; private set; }

    /// <summary>
    /// 加入 dotnet run 命令列引數。
    /// </summary>
    /// <param name="arguments">
    /// 要加入引數的集合。
    /// </param>
    /// <param name="options">
    /// 冒煙測試選項。
    /// </param>
    /// <param name="projectPath">
    /// 要執行的範例專案路徑。
    /// </param>
    public void AddRunArguments(ICollection<string> arguments, SmokeOptions options, string projectPath)
    {
        arguments.Add("run");
        arguments.Add("--project");
        arguments.Add(projectPath);
        arguments.Add("--framework");
        arguments.Add(Framework);
        arguments.Add("--configuration");
        arguments.Add(options.Configuration);
        if (!string.IsNullOrWhiteSpace(RuntimeIdentifier))
        {
            arguments.Add("--runtime");
            arguments.Add(RuntimeIdentifier!);
        }

        if (options.NoBuild)
        {
            arguments.Add("--no-build");
        }
    }

    /// <summary>
    /// 依範例根目錄解析專案路徑。
    /// </summary>
    /// <param name="sampleRoot">
    /// 包含 `samples` 資料夾的根目錄。
    /// </param>
    /// <returns>
    /// 範例專案完整路徑。
    /// </returns>
    public string ResolveProjectPath(string sampleRoot)
    {
        if (Path.IsPathRooted(ProjectPath))
        {
            return ProjectPath;
        }

        return Path.GetFullPath(Path.Combine(sampleRoot, ProjectPath));
    }
}

/// <summary>
/// 提供範例清單。
/// </summary>
internal static class SmokeSampleCatalog
{
    /// <summary>
    /// 可執行冒煙測試的全部範例。
    /// </summary>
    private static readonly IReadOnlyList<SmokeSample> Samples = new[]
    {
        new SmokeSample("WinForms", "samples/WinFormsSample/MediaEmbedKit.Mpv.Samples.WinForms.csproj", "net10.0-windows", null),
        new SmokeSample("WPF", "samples/WpfSample/MediaEmbedKit.Mpv.Samples.Wpf.csproj", "net10.0-windows", null),
        new SmokeSample("Avalonia", "samples/AvaloniaSample/MediaEmbedKit.Mpv.Samples.Avalonia.csproj", "net10.0-windows", null),
        new SmokeSample("WinUI", "samples/WinUISample/MediaEmbedKit.Mpv.Samples.WinUI.csproj", "net10.0-windows10.0.19041.0", "win-x64"),
        new SmokeSample("MAUI", "samples/MauiSample/MediaEmbedKit.Mpv.Samples.Maui.csproj", "net10.0-windows10.0.19041.0", "win-x64")
    };

    /// <summary>
    /// 依指定名稱取得範例清單。
    /// </summary>
    /// <param name="sampleName">
    /// 範例名稱或 <c>all</c>。
    /// </param>
    /// <returns>
    /// 符合條件的範例清單。
    /// </returns>
    public static IReadOnlyList<SmokeSample> GetSamples(string sampleName)
    {
        if (string.Equals(sampleName, "all", StringComparison.OrdinalIgnoreCase))
        {
            return Samples;
        }

        List<SmokeSample> matches = new List<SmokeSample>();
        foreach (SmokeSample sample in Samples)
        {
            if (string.Equals(sample.Name, sampleName, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(sample);
            }
        }

        return matches;
    }
}
