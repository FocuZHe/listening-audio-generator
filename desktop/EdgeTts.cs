using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace ListeningAudio;

static class SecMsGec
{
    const string ChromiumVersion = "143.0.3650.96";
    const string TrustedToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    const string Copyright = "Copyright (C) Microsoft Corporation. All rights reserved.";
    const long WindowsFileTimeEpoch = 11644473600L;

    static readonly uint[] MixFirst = { 0xCF98B111, 0x87085B9F, 0x12CEB96D, 0x257E1D83 };
    static readonly uint[] MixSecond = { 0xA27416F5, 0xD38396FF, 0x7C932B89, 0xBFA49F69 };

    public static string Token()
    {
        long ticks = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + WindowsFileTimeEpoch) * 10000000L;
        long rounded = ticks - (ticks % 3000000000L);
        byte[] message = Encoding.UTF8.GetBytes(Copyright + rounded + TrustedToken);
        byte[] digest = SHA256.HashData(message);
        return ToHex(Cs64(message, digest)) + "-" + ToHex(digest);
    }

    public static string Version => "2-" + ChromiumVersion;

    static byte[] Cs64(byte[] message, byte[] digest)
    {
        uint firstKey = (BitConverter.ToUInt32(digest, 0) | 1);
        uint secondKey = (BitConverter.ToUInt32(digest, 4) | 1);
        int paddedLength = ((message.Length + 7) / 8) * 8;
        byte[] padded = new byte[paddedLength];
        Buffer.BlockCopy(message, 0, padded, 0, message.Length);
        uint swapSum = 0;
        uint lastSwap = 0;
        uint mix = 0;
        uint mixSum = 0;
        for (int offset = 0; offset < padded.Length; offset += 8)
        {
            uint firstWord = BitConverter.ToUInt32(padded, offset);
            uint secondWord = BitConverter.ToUInt32(padded, offset + 4);
            lastSwap = SwapTerm(secondWord, secondKey, 0xC31713DB, 0x1881CE3C, 0x35BD1EC9);
            swapSum += SwapTerm(firstWord, firstKey, 0xEF0569FB, 0x76F50F34, 0xC3EFEA97) + lastSwap;
            mix = SwapAndMultiply(Mul(mix + firstWord, firstKey), MixFirst);
            mixSum += mix;
            mix = SwapAndMultiply(Mul(mix + secondWord, secondKey), MixSecond);
            mixSum += mix;
        }
        byte[] output = new byte[8];
        WriteU32(output, 0, lastSwap ^ mix);
        WriteU32(output, 4, swapSum ^ mixSum);
        return output;
    }

    static uint SwapTerm(uint word, uint key, uint swappedWord, uint swappedSum, uint swappedProduct)
    {
        long sum = (long)(int)Mul(word, key) + (int)Mul(SwapHalves(word), swappedWord);
        uint product = Mul(SwapHalves(Low32(sum)), swappedSum);
        return Mul(SwapHalves(product), swappedProduct) + product;
    }

    static uint SwapAndMultiply(uint value, uint[] multipliers)
    {
        uint current = value;
        foreach (uint multiplier in multipliers)
            current = Mul(SwapHalves(current), multiplier);
        return current;
    }

    static uint SwapHalves(uint value) => (value << 16) | (value >> 16);

    static uint Mul(uint left, uint right) => unchecked((uint)((int)left * (int)right));

    static uint Low32(long value) => unchecked((uint)value);

    static void WriteU32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    static string ToHex(byte[] bytes) => Convert.ToHexString(bytes);
}

sealed class EdgeTts
{
    public async Task<byte[]> SynthesizeAsync(string text, string voiceId, double speed, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("没有可朗读的文字。");
        string connectionId = Guid.NewGuid().ToString("N");
        string url = "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1" +
            "?TrustedClientToken=6A5AA1D4EAFF4E9FB37E23D68491D6F4" +
            "&Sec-MS-GEC=" + SecMsGec.Token() +
            "&Sec-MS-GEC-Version=" + SecMsGec.Version +
            "&ConnectionId=" + connectionId;
        using var socket = new ClientWebSocket();
        TryHeader(socket, "Pragma", "no-cache");
        TryHeader(socket, "Cache-Control", "no-cache");
        TryHeader(socket, "Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
        TryHeader(socket, "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0");
        TryHeader(socket, "Accept-Language", "en-US,en;q=0.9");
        using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectTimeout.CancelAfter(TimeSpan.FromSeconds(20));
        await socket.ConnectAsync(new Uri(url), connectTimeout.Token);
        string lang = LanguageOf(voiceId);
        string rate = RateOf(speed);
        await SendTextAsync(socket, "Content-Type:application/json; charset=utf-8\r\nPath:speech.config\r\n\r\n" +
            "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"false\",\"wordBoundaryEnabled\":\"false\"},\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}", cancellationToken);
        string requestId = Guid.NewGuid().ToString("N");
        string ssml = "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"" + lang + "\">" +
            "<voice name=\"" + voiceId + "\"><prosody rate=\"" + rate + "\" pitch=\"default\" volume=\"default\">" +
            EscapeXml(text) + "</prosody></voice></speak>";
        await SendTextAsync(socket, "X-RequestId:" + requestId + "\r\nContent-Type:application/ssml+xml\r\nPath:ssml\r\n\r\n" + ssml, cancellationToken);
        using var audio = new MemoryStream();
        var buffer = new byte[16384];
        using var message = new MemoryStream();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (socket.State == WebSocketState.Open)
        {
            message.SetLength(0);
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, timeout.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                    throw new InvalidOperationException("语音服务提前关闭了连接。");
                message.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);
            byte[] payload = message.ToArray();
            if (result.MessageType == WebSocketMessageType.Text)
            {
                string textMessage = Encoding.UTF8.GetString(payload);
                if (textMessage.Contains("Path:turn.end"))
                    break;
            }
            else
            {
                AppendAudio(audio, payload);
            }
        }
        if (audio.Length < 64)
            throw new InvalidOperationException("音色 " + voiceId + " 没有返回音频。");
        try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); } catch { }
        return audio.ToArray();
    }

    static void AppendAudio(MemoryStream audio, byte[] payload)
    {
        byte[] marker = Encoding.ASCII.GetBytes("Path:audio\r\n");
        int index = IndexOf(payload, marker);
        if (index < 0) return;
        int start = index + marker.Length;
        if (start < payload.Length)
            audio.Write(payload, start, payload.Length - start);
    }

    static int IndexOf(byte[] data, byte[] pattern)
    {
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    static async Task SendTextAsync(ClientWebSocket socket, string text, CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    static void TryHeader(ClientWebSocket socket, string name, string value)
    {
        try { socket.Options.SetRequestHeader(name, value); } catch { }
    }

    public static string LanguageOf(string voiceId)
    {
        string[] parts = (voiceId ?? "").Split('-');
        if (parts.Length >= 2) return parts[0] + "-" + parts[1];
        return "en-US";
    }

    public static string RateOf(double speed)
    {
        if (Math.Abs(speed - 1) < 0.001) return "default";
        int percent = (int)Math.Round((speed - 1) * 100);
        return percent >= 0 ? "+" + percent + "%" : percent + "%";
    }

    static string EscapeXml(string value)
    {
        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
    }
}
