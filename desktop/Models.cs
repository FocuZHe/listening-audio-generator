using System.Text.Json;
using System.Text.Json.Serialization;

namespace ListeningAudio;

static class VoiceDefaults
{
    public const string Male = "Microsoft WilliamMultilingual Online (Natural)";
    public const string Female = "Microsoft Natasha Online (Natural) - English (Australia)";
    public const string Chinese = "Microsoft Xiaoxiao Online (Natural) - Chinese (Mainland)";
    public const string Footer = "听力部分到此结束，请接着完成笔试部分。";
}

sealed class Store
{
    public List<Archive> archives { get; set; } = new();
    public Settings config { get; set; } = Settings.CreateDefault();
    public string current_archive_id { get; set; }

    public static Store CreateNew()
    {
        var archive = Archive.Create("听力材料");
        return new Store
        {
            archives = new List<Archive> { archive },
            config = Settings.CreateDefault(),
            current_archive_id = archive.id
        };
    }
}

sealed class Archive
{
    public string id { get; set; }
    public string name { get; set; }
    public List<Section> sections { get; set; } = new();
    public string examHeader { get; set; } = "";
    public string examFooter { get; set; } = VoiceDefaults.Footer;

    public static Archive Create(string name)
    {
        return new Archive
        {
            id = "archive-" + Ids.Now(),
            name = name,
            examHeader = "",
            examFooter = VoiceDefaults.Footer
        };
    }
}

sealed class Section
{
    public string id { get; set; }
    public string title { get; set; } = "";
    public string subtitle { get; set; } = "";
    public List<Question> items { get; set; } = new();

    public static Section Create()
    {
        return new Section { id = "section-" + Ids.Now() };
    }
}

sealed class Question
{
    public string id { get; set; }
    public string num { get; set; } = "1";
    public string type { get; set; } = "statement";
    public string introText { get; set; } = "";
    public string text { get; set; } = "";
    public List<Line> dialogue { get; set; }

    public static Question Statement(string num)
    {
        return new Question
        {
            id = "q-" + Ids.Now(),
            num = num,
            type = "statement",
            introText = "Number " + num + ".",
            text = ""
        };
    }

    public static Question Dialogue(string num)
    {
        var stamp = Ids.Now();
        return new Question
        {
            id = "q-" + stamp,
            num = num,
            type = "dialogue",
            introText = "Number " + num + ".",
            dialogue = new List<Line>
            {
                new Line { id = "dl-" + stamp + "-1", speaker = "A", text = "", gender = "male" },
                new Line { id = "dl-" + stamp + "-2", speaker = "B", text = "", gender = "female" }
            }
        };
    }
}

sealed class Line
{
    public string id { get; set; }
    public string speaker { get; set; } = "A";
    public string text { get; set; } = "";
    public string gender { get; set; } = "male";
}

sealed class Settings
{
    public double speed { get; set; } = 0.9;
    public string browserMaleVoice { get; set; } = VoiceDefaults.Male;
    public string browserFemaleVoice { get; set; } = VoiceDefaults.Female;
    public string browserChineseVoice { get; set; } = VoiceDefaults.Chinese;
    public double pauseAfterLine { get; set; } = 1.5;
    public double pauseAfterDialogueLine { get; set; } = 0.1;
    public double pauseAfterQuestion { get; set; } = 4;
    public bool playChime { get; set; } = true;
    public string examHeader { get; set; } = "";
    public string examFooter { get; set; } = VoiceDefaults.Footer;

    public static Settings CreateDefault()
    {
        return new Settings();
    }
}

static class JsonStore
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

}

static class Ids
{
    public static string Now()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
    }
}
