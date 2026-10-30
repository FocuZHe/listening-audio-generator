using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ListeningAudio;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--self-test")
            return SelfTest(all: false);
        if (args.Length > 0 && args[0] == "--self-test-all")
            return SelfTest(all: true);
        ApplicationConfiguration.Initialize();
        Application.Run(new WebHostForm());
        return 0;
    }

    static int SelfTest(bool all)
    {
        using var api = new LocalApi();
        api.Start();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
        string health = client.GetStringAsync(api.RootUrl + "api/health").GetAwaiter().GetResult();
        if (!health.Contains("ok")) throw new InvalidOperationException(health);
        string voices = client.GetStringAsync(api.RootUrl + "api/voices").GetAwaiter().GetResult();
        using var document = System.Text.Json.JsonDocument.Parse(voices);
        var names = document.RootElement.GetProperty("voices").EnumerateObject().Select(property => property.Name).ToList();
        if (!all)
        {
            names = new List<string>
            {
                "Microsoft WilliamMultilingual Online (Natural)",
                "Microsoft Natasha Online (Natural) - English (Australia)",
                "Microsoft Xiaoxiao Online (Natural) - Chinese (Mainland)"
            };
        }
        int ok = 0;
        var failed = new List<string>();
        int cursor = 0;
        var gate = new object();
        Task.WaitAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (true)
            {
                int index;
                lock (gate)
                {
                    if (cursor >= names.Count) return;
                    index = cursor++;
                }
                string name = names[index];
                string text = name.Contains("Xiaoxiao") || name.Contains("Chinese") ? "你好。" : "Hello.";
                try
                {
                    var content = new StringContent("{\"text\":" + System.Text.Json.JsonSerializer.Serialize(text) + ",\"voiceName\":" + System.Text.Json.JsonSerializer.Serialize(name) + ",\"role\":\"narrator\",\"speed\":1}", System.Text.Encoding.UTF8, "application/json");
                    var response = client.PostAsync(api.RootUrl + "api/tts", content).GetAwaiter().GetResult();
                    string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode || !body.Contains("\"audio\""))
                        throw new InvalidOperationException(response.StatusCode + " " + body.Substring(0, Math.Min(180, body.Length)));
                    lock (gate) ok++;
                    Console.WriteLine("OK " + (index + 1) + "/" + names.Count + " " + name);
                }
                catch (Exception ex)
                {
                    lock (gate) failed.Add(name + " " + ex.Message);
                    Console.WriteLine("FAIL " + name + " " + ex.Message);
                }
            }
        })).ToArray());
        Console.WriteLine("DONE ok=" + ok + " failed=" + failed.Count);
        foreach (string line in failed) Console.WriteLine(line);
        return failed.Count == 0 ? 0 : 1;
    }
}

sealed class WebHostForm : Form
{
    readonly LocalApi _api = new();
    readonly WebView2 _view = new() { Dock = DockStyle.Fill };

    public WebHostForm()
    {
        Text = "听力音频生成器";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1280, 860);
        MinimumSize = new Size(960, 640);
        Controls.Add(_view);
        Load += async (_, _) => await OpenAsync();
        FormClosing += (_, _) => _api.Dispose();
    }

    async Task OpenAsync()
    {
        try
        {
            _api.Start();
            string profile = Path.Combine(AppPaths.Directory, "webview-profile");
            Directory.CreateDirectory(profile);
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, profile);
            await _view.EnsureCoreWebView2Async(environment);
            _view.CoreWebView2.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Light;
            _view.CoreWebView2.DownloadStarting += OnDownloadStarting;
            _view.CoreWebView2.Navigate(_api.RootUrl);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "窗口版需要 Microsoft Edge WebView2 运行库。\n\n" +
                "Windows 11 通常已经自带。\n" +
                "Windows 10 如果打不开，请先安装 Microsoft Edge，或到下面的页面下载 Evergreen 运行库：\n" +
                "https://developer.microsoft.com/microsoft-edge/webview2/\n\n" +
                ex.Message,
                "无法打开窗口");
            Close();
        }
    }

    void OnDownloadStarting(object sender, CoreWebView2DownloadStartingEventArgs args)
    {
        var deferral = args.GetDeferral();
        string suggested = Path.GetFileName(string.IsNullOrWhiteSpace(args.ResultFilePath) ? "听力音频.wav" : args.ResultFilePath);
        BeginInvoke(new Action(() =>
        {
            try
            {
                using var dialog = new SaveFileDialog
                {
                    Title = "选择整卷音频的保存位置",
                    Filter = "WAV 音频|*.wav|所有文件|*.*",
                    FileName = suggested,
                    AddExtension = true,
                    OverwritePrompt = true
                };
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    args.ResultFilePath = dialog.FileName;
                else
                    args.Cancel = true;
            }
            finally
            {
                deferral.Complete();
            }
        }));
    }
}
