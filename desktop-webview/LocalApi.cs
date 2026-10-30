using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ListeningAudio;

static class AppPaths
{
    public static string Directory
    {
        get
        {
            string nextToExe = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            try
            {
                string probe = Path.Combine(nextToExe, ".write-test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return nextToExe;
            }
            catch
            {
                string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ListeningAudioGenerator");
                System.IO.Directory.CreateDirectory(local);
                return local;
            }
        }
    }

    public static string DataFile => Path.Combine(Directory, "data.json");
}

sealed class LocalApi : IDisposable
{
    readonly HttpListener _listener = new();
    readonly CancellationTokenSource _cancel = new();
    readonly EdgeTts _tts = new();
    readonly Dictionary<string, string> _voices;
    readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);
    int _port;

    public int Port => _port;
    public string RootUrl => "http://127.0.0.1:" + _port + "/";

    public LocalApi()
    {
        _voices = LoadVoices();
        LoadWebFiles();
    }

    public void Start()
    {
        _port = FreePort();
        _listener.Prefixes.Add(RootUrl);
        _listener.Start();
        Task.Run(Loop);
    }

    async Task Loop()
    {
        while (!_cancel.IsCancellationRequested)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); }
            catch { break; }
            _ = Task.Run(() => Handle(context));
        }
    }

    void Handle(HttpListenerContext context)
    {
        try
        {
            string path = context.Request.Url.AbsolutePath;
            if (path == "/api/health")
            {
                Json(context, 200, new { status = "ok", time = DateTime.UtcNow.ToString("o") });
                return;
            }
            if (path == "/api/voices")
            {
                Json(context, 200, new { voices = _voices, groups = Groups() });
                return;
            }
            if (path == "/api/data" && context.Request.HttpMethod == "GET")
            {
                Json(context, 200, new { success = true, data = ReadData() });
                return;
            }
            if (path == "/api/data" && context.Request.HttpMethod == "POST")
            {
                MergeData(ReadBody(context));
                Json(context, 200, new { success = true });
                return;
            }
            if (path == "/api/tts" && context.Request.HttpMethod == "POST")
            {
                Speak(context);
                return;
            }
            if (_files.TryGetValue(path, out byte[] file) || (path == "/" && _files.TryGetValue("/index.html", out file)))
            {
                Bytes(context, 200, file, ContentType(path == "/" ? "/index.html" : path));
                return;
            }
            if (path.StartsWith("/api/"))
            {
                Json(context, 404, new { error = "not_found" });
                return;
            }
            Bytes(context, 200, _files["/index.html"], "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            try { Json(context, 500, new { error = "server_error", message = ex.Message }); } catch { }
        }
    }

    void Speak(HttpListenerContext context)
    {
        using var document = JsonDocument.Parse(ReadBody(context));
        JsonElement root = document.RootElement;
        string text = root.TryGetProperty("text", out JsonElement textElement) ? textElement.GetString() : "";
        if (string.IsNullOrWhiteSpace(text))
        {
            Json(context, 400, new { error = "text is required" });
            return;
        }
        string voiceName = root.TryGetProperty("voiceName", out JsonElement voiceElement) ? voiceElement.GetString() : "";
        double speed = 1;
        if (root.TryGetProperty("speed", out JsonElement speedElement) && speedElement.ValueKind == JsonValueKind.Number)
            speed = speedElement.GetDouble();
        string voiceId = ResolveVoice(voiceName, text);
        byte[] audio = _tts.SynthesizeAsync(text, voiceId, speed, CancellationToken.None).GetAwaiter().GetResult();
        Json(context, 200, new { audio = Convert.ToBase64String(audio) });
    }

    string ResolveVoice(string voiceName, string text)
    {
        if (!string.IsNullOrWhiteSpace(voiceName) && _voices.TryGetValue(voiceName, out string id))
            return id;
        foreach (char character in text ?? "")
        {
            if (character >= 0x4e00 && character <= 0x9fff) return "zh-CN-XiaoxiaoNeural";
        }
        return "en-US-JennyNeural";
    }

    Dictionary<string, List<VoiceItem>> Groups()
    {
        var groups = new Dictionary<string, List<VoiceItem>>();
        foreach (var pair in _voices)
        {
            string[] parts = pair.Value.Split('-');
            string lang = parts.Length >= 2 ? parts[0] + "-" + parts[1] : pair.Value;
            if (!groups.TryGetValue(lang, out List<VoiceItem> list))
            {
                list = new List<VoiceItem>();
                groups[lang] = list;
            }
            list.Add(new VoiceItem { name = pair.Key, id = pair.Value });
        }
        return groups;
    }

    static Dictionary<string, string> LoadVoices()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("voice_map.json");
        using var reader = new StreamReader(stream);
        using var document = JsonDocument.Parse(reader.ReadToEnd());
        var map = new Dictionary<string, string>();
        foreach (var property in document.RootElement.EnumerateObject())
            map[property.Name] = property.Value.GetString();
        return map;
    }

    void LoadWebFiles()
    {
        foreach (string name in Assembly.GetExecutingAssembly().GetManifestResourceNames())
        {
            if (!name.StartsWith("web/")) continue;
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            _files["/" + name.Substring("web/".Length)] = memory.ToArray();
        }
        if (_files.ContainsKey("/index.html"))
            _files["/"] = _files["/index.html"];
    }

    JsonElement ReadData()
    {
        if (!File.Exists(AppPaths.DataFile))
            return JsonDocument.Parse("{\"archives\":[],\"config\":null,\"current_archive_id\":null}").RootElement.Clone();
        return JsonDocument.Parse(File.ReadAllText(AppPaths.DataFile)).RootElement.Clone();
    }

    void MergeData(string body)
    {
        JsonElement incoming = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body).RootElement;
        JsonElement existing = ReadData();
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            WriteMerged(writer, "archives", incoming, existing);
            WriteMerged(writer, "config", incoming, existing);
            WriteMerged(writer, "current_archive_id", incoming, existing);
            writer.WriteEndObject();
        }
        Directory.CreateDirectory(AppPaths.Directory);
        string temp = AppPaths.DataFile + ".tmp";
        File.WriteAllBytes(temp, stream.ToArray());
        File.Move(temp, AppPaths.DataFile, true);
    }

    static void WriteMerged(Utf8JsonWriter writer, string name, JsonElement incoming, JsonElement existing)
    {
        writer.WritePropertyName(name);
        if (incoming.TryGetProperty(name, out JsonElement value))
            value.WriteTo(writer);
        else if (existing.ValueKind == JsonValueKind.Object && existing.TryGetProperty(name, out JsonElement current))
            current.WriteTo(writer);
        else
            writer.WriteNullValue();
    }

    static string ReadBody(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8);
        return reader.ReadToEnd();
    }

    static void Json(HttpListenerContext context, int status, object value)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        Bytes(context, status, bytes, "application/json; charset=utf-8");
    }

    static void Bytes(HttpListenerContext context, int status, byte[] bytes, string type)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = type;
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        context.Response.OutputStream.Close();
    }

    static string ContentType(string path)
    {
        if (path.EndsWith(".js")) return "text/javascript; charset=utf-8";
        if (path.EndsWith(".css")) return "text/css; charset=utf-8";
        return "text/html; charset=utf-8";
    }

    static int FreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        _cancel.Cancel();
        try { _listener.Stop(); } catch { }
        _listener.Close();
    }
}

sealed class VoiceItem
{
    public string name { get; set; }
    public string id { get; set; }
}
