using System.Reflection;
using System.Text.Json;
using NAudio.Wave;
using NLayer;

namespace ListeningAudio;

sealed class VoiceEntry
{
    public string Name;
    public string Id;
}

sealed class VoiceCatalog
{
    public readonly List<VoiceEntry> All = new();

    public static VoiceCatalog Load()
    {
        string beside = Path.Combine(AppPaths.Directory, "voice_map.json");
        string json = File.Exists(beside)
            ? File.ReadAllText(beside)
            : ReadEmbedded();
        var catalog = new VoiceCatalog();
        using var document = JsonDocument.Parse(json);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            catalog.All.Add(new VoiceEntry { Name = property.Name, Id = property.Value.GetString() });
        }
        return catalog;
    }

    static string ReadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("voice_map.json");
        if (stream == null) throw new InvalidOperationException("程序里没有音色表。");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public IEnumerable<string> EnglishNames()
    {
        return All.Where(entry => entry.Id.StartsWith("en-US-") || entry.Id.StartsWith("en-AU-") || entry.Id.StartsWith("en-GB-"))
            .Select(entry => entry.Name);
    }

    public IEnumerable<string> NarratorNames()
    {
        return All.Where(entry =>
                entry.Id.StartsWith("en-US-") || entry.Id.StartsWith("en-AU-") || entry.Id.StartsWith("en-GB-") ||
                entry.Id.StartsWith("zh-CN-") || entry.Id.StartsWith("zh-TW-") || entry.Id.StartsWith("zh-HK-"))
            .Select(entry => entry.Name);
    }

    public string Resolve(string displayName, string role, string text, Settings settings)
    {
        string chosen = displayName;
        if (string.IsNullOrWhiteSpace(chosen))
        {
            if (role == "male") chosen = settings.browserMaleVoice;
            else if (role == "female") chosen = settings.browserFemaleVoice;
            else chosen = ContainsChinese(text) ? settings.browserChineseVoice : settings.browserFemaleVoice;
        }
        VoiceEntry exact = All.FirstOrDefault(entry => entry.Name == chosen);
        if (exact != null) return exact.Id;
        if (role == "male") return "en-US-BrianNeural";
        if (ContainsChinese(text)) return "zh-CN-XiaoxiaoNeural";
        return "en-US-JennyNeural";
    }

    public static bool ContainsChinese(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (char character in text)
        {
            if (character >= 0x4e00 && character <= 0x9fff) return true;
        }
        return false;
    }
}

static class AppPaths
{
    public static string Directory =>
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    public static string DataFile => Path.Combine(Directory, "data.json");
}

sealed class LocalStore
{
    public Store Data = Store.CreateNew();

    public void Load()
    {
        if (!File.Exists(AppPaths.DataFile))
        {
            Data = Store.CreateNew();
            Save();
            return;
        }
        try
        {
            Data = JsonSerializer.Deserialize<Store>(File.ReadAllText(AppPaths.DataFile), JsonStore.Options) ?? Store.CreateNew();
        }
        catch
        {
            Data = Store.CreateNew();
        }
        if (Data.archives == null || Data.archives.Count == 0)
            Data.archives = Store.CreateNew().archives;
        Data.config ??= Settings.CreateDefault();
        if (string.IsNullOrWhiteSpace(Data.config.browserMaleVoice)) Data.config.browserMaleVoice = VoiceDefaults.Male;
        if (string.IsNullOrWhiteSpace(Data.config.browserFemaleVoice)) Data.config.browserFemaleVoice = VoiceDefaults.Female;
        if (string.IsNullOrWhiteSpace(Data.config.browserChineseVoice)) Data.config.browserChineseVoice = VoiceDefaults.Chinese;
        if (Data.archives.All(archive => archive.id != Data.current_archive_id))
            Data.current_archive_id = Data.archives[0].id;
    }

    public void Save()
    {
        Directory.CreateDirectory(AppPaths.Directory);
        string temp = AppPaths.DataFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Data, JsonStore.Options));
        File.Move(temp, AppPaths.DataFile, true);
    }

    public Archive Current()
    {
        return Data.archives.FirstOrDefault(archive => archive.id == Data.current_archive_id) ?? Data.archives[0];
    }
}

enum SegmentKind { Speech, Silence, Chime }

sealed class Segment
{
    public SegmentKind Kind;
    public string Text;
    public string VoiceId;
    public double Seconds;
    public string Description;
}

static class Timeline
{
    public static List<Segment> FullExam(Archive archive, Settings settings, VoiceCatalog voices)
    {
        var list = new List<Segment>();
        AddSpeech(list, archive.examHeader, "narrator", settings, voices, 1.8, "开场白");
        Section previous = null;
        foreach (Section section in archive.sections ?? new List<Section>())
        {
            string title = SectionTitle(section, previous);
            AddSpeech(list, title, "narrator", settings, voices, 2.2, "大题引导");
            foreach (Question question in section.items ?? new List<Question>())
                AddQuestion(list, question, settings, voices);
            previous = section;
        }
        AddSpeech(list, archive.examFooter, "narrator", settings, voices, 0, "结束语");
        return list;
    }

    public static List<Segment> OneQuestion(Question question, Settings settings, VoiceCatalog voices)
    {
        var list = new List<Segment>();
        AddQuestion(list, question, settings, voices);
        return list;
    }

    static void AddQuestion(List<Segment> list, Question question, Settings settings, VoiceCatalog voices)
    {
        if (question == null) return;
        if (settings.playChime)
            list.Add(new Segment { Kind = SegmentKind.Chime, Description = "提示音" });
        AddSpeech(list, question.introText, "narrator", settings, voices, 0.6, "题号");
        if (question.type == "dialogue" && question.dialogue != null)
        {
            AddDialogueRound(list, question, settings, voices);
            list.Add(new Segment { Kind = SegmentKind.Silence, Seconds = 1.8, Description = "重复间隔" });
            AddDialogueRound(list, question, settings, voices);
            list.Add(new Segment { Kind = SegmentKind.Silence, Seconds = Math.Max(0.01, settings.pauseAfterQuestion), Description = "答题留白" });
            return;
        }
        AddSpeech(list, question.text, "male", settings, voices, settings.pauseAfterLine, "男声");
        AddSpeech(list, question.text, "female", settings, voices, settings.pauseAfterQuestion, "女声");
    }

    static void AddDialogueRound(List<Segment> list, Question question, Settings settings, VoiceCatalog voices)
    {
        for (int i = 0; i < question.dialogue.Count; i++)
        {
            Line line = question.dialogue[i];
            bool last = i == question.dialogue.Count - 1;
            string role = line.gender == "female" ? "female" : "male";
            AddSpeech(list, line.text, role, settings, voices, last ? 0 : settings.pauseAfterDialogueLine, "对话");
        }
    }

    static void AddSpeech(List<Segment> list, string text, string role, Settings settings, VoiceCatalog voices, double pauseAfter, string description)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        string display = role == "male" ? settings.browserMaleVoice : role == "female" ? settings.browserFemaleVoice : NarratorName(text, settings);
        list.Add(new Segment
        {
            Kind = SegmentKind.Speech,
            Text = text.Trim(),
            VoiceId = voices.Resolve(display, role, text, settings),
            Description = description
        });
        if (pauseAfter > 0)
            list.Add(new Segment { Kind = SegmentKind.Silence, Seconds = Math.Max(0.01, pauseAfter), Description = "停顿" });
    }

    static string NarratorName(string text, Settings settings)
    {
        return VoiceCatalog.ContainsChinese(text) ? settings.browserChineseVoice : settings.browserFemaleVoice;
    }

    static string SectionTitle(Section section, Section previous)
    {
        string title = (section.title ?? "").Trim();
        string subtitle = section.subtitle ?? "";
        if (previous != null && title == (previous.title ?? "").Trim())
            return (subtitle ?? "").Trim();
        return (title + " " + (subtitle ?? "")).Trim();
    }
}

static class AudioMixer
{
    public const int SampleRate = 24000;

    public static byte[] Chime()
    {
        int length = (int)(SampleRate * 1.8);
        var samples = new float[length];
        const double first = 659.25;
        const double second = 523.25;
        for (int i = 0; i < length; i++)
        {
            double time = i / (double)SampleRate;
            double high = 0;
            if (time < 1)
            {
                double envelope = Math.Exp(-4 * time);
                high = (Math.Sin(2 * Math.PI * first * time) * 0.4 + Math.Sin(2 * Math.PI * first * 2 * time) * 0.1) * envelope;
            }
            double low = 0;
            if (time >= 0.7)
            {
                double shifted = time - 0.7;
                double envelope = Math.Exp(-3.5 * shifted);
                low = (Math.Sin(2 * Math.PI * second * shifted) * 0.4 + Math.Sin(2 * Math.PI * second * 2 * shifted) * 0.1) * envelope;
            }
            samples[i] = (float)((high + low) * 0.8);
        }
        return ToPcm(samples);
    }

    public static byte[] Silence(double seconds)
    {
        int count = Math.Max(1, (int)Math.Round(SampleRate * Math.Max(0.01, seconds)));
        return new byte[count * 2];
    }

    public static byte[] DecodeMp3(byte[] mp3)
    {
        try
        {
            return Decode(mp3);
        }
        catch
        {
            int start = FindFrame(mp3);
            if (start < 0) throw;
            byte[] trimmed = new byte[mp3.Length - start];
            Buffer.BlockCopy(mp3, start, trimmed, 0, trimmed.Length);
            return Decode(trimmed);
        }
    }

    static byte[] Decode(byte[] mp3)
    {
        using var mpeg = new MpegFile(new MemoryStream(mp3));
        var floats = new List<float>();
        var buffer = new float[4096];
        int read;
        while ((read = mpeg.ReadSamples(buffer, 0, buffer.Length)) > 0)
        {
            int channels = Math.Max(1, mpeg.Channels);
            for (int i = 0; i < read; i += channels)
                floats.Add(buffer[i]);
        }
        if (mpeg.SampleRate == SampleRate)
            return ToPcm(floats);
        return ToPcm(Resample(floats, mpeg.SampleRate, SampleRate));
    }

    static List<float> Resample(List<float> input, int from, int to)
    {
        if (from <= 0 || input.Count == 0) return input;
        int outputCount = Math.Max(1, (int)Math.Round(input.Count * (to / (double)from)));
        var output = new List<float>(outputCount);
        for (int i = 0; i < outputCount; i++)
        {
            double position = i * (from / (double)to);
            int index = Math.Min(input.Count - 1, (int)position);
            output.Add(input[index]);
        }
        return output;
    }

    static int FindFrame(byte[] data)
    {
        for (int i = 0; i < data.Length - 1; i++)
        {
            if (data[i] == 0xFF && (data[i + 1] & 0xE0) == 0xE0) return i;
        }
        return -1;
    }

    public static byte[] ToPcm(IList<float> samples)
    {
        var bytes = new byte[samples.Count * 2];
        for (int i = 0; i < samples.Count; i++)
        {
            float sample = Math.Clamp(samples[i], -1f, 1f);
            short value = (short)Math.Round(sample < 0 ? sample * 32768f : sample * 32767f);
            bytes[i * 2] = (byte)value;
            bytes[i * 2 + 1] = (byte)(value >> 8);
        }
        return bytes;
    }

    public static byte[] Concat(IEnumerable<byte[]> parts)
    {
        int length = parts.Sum(part => part.Length);
        var output = new byte[length];
        int offset = 0;
        foreach (byte[] part in parts)
        {
            Buffer.BlockCopy(part, 0, output, offset, part.Length);
            offset += part.Length;
        }
        return output;
    }

    public static void WriteWav(string path, byte[] pcm)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        int dataLength = pcm.Length;
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
        writer.Write(pcm);
    }
}

sealed class SoundPlayer : IDisposable
{
    WaveOutEvent _output;

    public async Task PlayAsync(byte[] pcm, CancellationToken cancellationToken)
    {
        Stop();
        var format = new WaveFormat(AudioMixer.SampleRate, 16, 1);
        var stream = new RawSourceWaveStream(new MemoryStream(pcm), format);
        var output = new WaveOutEvent();
        output.Init(stream);
        _output = output;
        output.Play();
        try
        {
            while (output.PlaybackState == PlaybackState.Playing)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(40, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            try { output.Stop(); } catch { }
            throw;
        }
        finally
        {
            output.Dispose();
            stream.Dispose();
            if (ReferenceEquals(_output, output)) _output = null;
        }
    }

    public void Stop()
    {
        try { _output?.Stop(); } catch { }
    }

    public void Dispose() => Stop();
}
