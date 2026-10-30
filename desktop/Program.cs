namespace ListeningAudio;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--self-test-all")
            return SelfTestAll();
        if (args.Length > 0 && args[0] == "--self-test")
            return SelfTest();
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }

    static int SelfTest()
    {
        try
        {
            var voices = VoiceCatalog.Load();
            var settings = Settings.CreateDefault();
            var tts = new EdgeTts();
            string[] names = { settings.browserMaleVoice, settings.browserFemaleVoice, settings.browserChineseVoice };
            foreach (string name in names)
            {
                string id = voices.Resolve(name, name == settings.browserMaleVoice ? "male" : "narrator", "Hello", settings);
                byte[] audio = tts.SynthesizeAsync(name == settings.browserChineseVoice ? "你好。" : "Hello.", id, settings.speed, CancellationToken.None)
                    .GetAwaiter().GetResult();
                if (audio.Length < 200)
                    throw new InvalidOperationException(name + " 音频过短。");
                byte[] pcm = AudioMixer.DecodeMp3(audio);
                if (pcm.Length < 200)
                    throw new InvalidOperationException(name + " 无法解码。");
                Console.WriteLine("OK " + id + " mp3=" + audio.Length + " pcm=" + pcm.Length);
            }
            byte[] chime = AudioMixer.Chime();
            string wav = Path.Combine(Path.GetTempPath(), "listening-chime-test.wav");
            AudioMixer.WriteWav(wav, chime);
            Console.WriteLine("WAV " + new FileInfo(wav).Length);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    static int SelfTestAll()
    {
        var voices = VoiceCatalog.Load();
        var settings = Settings.CreateDefault();
        var tts = new EdgeTts();
        int cursor = 0;
        int ok = 0;
        var failed = new List<string>();
        var gate = new object();
        Task[] workers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (true)
            {
                int index;
                lock (gate)
                {
                    if (cursor >= voices.All.Count) return;
                    index = cursor++;
                }
                VoiceEntry entry = voices.All[index];
                try
                {
                    byte[] audio = tts.SynthesizeAsync("Hello.", entry.Id, 1, CancellationToken.None).GetAwaiter().GetResult();
                    byte[] pcm = AudioMixer.DecodeMp3(audio);
                    if (audio.Length < 200 || pcm.Length < 200) throw new InvalidOperationException("音频过短");
                    lock (gate) ok++;
                    Console.WriteLine("OK " + (index + 1) + "/" + voices.All.Count + " " + entry.Id);
                }
                catch (Exception ex)
                {
                    lock (gate) failed.Add(entry.Name + " => " + entry.Id + " " + ex.Message);
                    Console.WriteLine("FAIL " + entry.Id + " " + ex.Message);
                }
            }
        })).ToArray();
        Task.WaitAll(workers);
        string report = "DONE ok=" + ok + " failed=" + failed.Count + Environment.NewLine + string.Join(Environment.NewLine, failed);
        Console.WriteLine(report);
        try
        {
            Directory.CreateDirectory(Path.Combine(AppPaths.Directory, "build"));
            File.WriteAllText(Path.Combine(AppPaths.Directory, "build", "native-voice-report.txt"), report);
        }
        catch { }
        return failed.Count == 0 ? 0 : 1;
    }
}
