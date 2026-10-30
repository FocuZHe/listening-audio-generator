namespace ListeningAudio;

sealed class MainForm : Form
{
    readonly LocalStore _store = new();
    readonly VoiceCatalog _voices;
    readonly EdgeTts _tts = new();
    readonly Dictionary<string, byte[]> _cache = new();
    readonly SoundPlayer _player = new();

    ComboBox _archives;
    ListBox _sections;
    ListBox _questions;
    TextBox _title;
    TextBox _subtitle;
    TextBox _header;
    TextBox _footer;
    TextBox _intro;
    TextBox _number;
    TextBox _statement;
    RadioButton _statementType;
    RadioButton _dialogueType;
    DataGridView _lines;
    Panel _statementPanel;
    Panel _dialoguePanel;
    TrackBar _speed;
    Label _speedLabel;
    TextBox _pauseLine;
    TextBox _pauseDialogue;
    TextBox _pauseQuestion;
    CheckBox _chime;
    ComboBox _male;
    ComboBox _female;
    ComboBox _narrator;
    Label _stats;
    Label _status;
    Label _dataPath;
    ProgressBar _progress;
    Button _pauseButton;
    bool _loading;
    bool _busy;
    CancellationTokenSource _play;
    int _resumeSection = -1;
    int _resumeQuestion = -1;

    public MainForm()
    {
        _voices = VoiceCatalog.Load();
        Text = "听力音频生成器";
        Font = new Font("Microsoft YaHei UI", 9F);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 720);
        Size = new Size(1180, 780);
        BuildUi();
        Load += (_, _) => OpenStore();
        FormClosing += (_, _) => SaveStore();
    }

    void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildLibrary(), 0, 0);
        root.Controls.Add(BuildEditor(), 1, 0);
        Controls.Add(root);
    }

    Control BuildLibrary()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 1 };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = "听力材料", AutoSize = true, Margin = new Padding(0, 0, 0, 4) });
        _archives = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        _archives.SelectedIndexChanged += (_, _) => { if (!_loading) SwitchArchive(); };
        panel.Controls.Add(_archives);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        buttons.Controls.Add(MakeButton("新建", (_, _) => CreateArchive()));
        buttons.Controls.Add(MakeButton("重命名", (_, _) => RenameArchive()));
        buttons.Controls.Add(MakeButton("删除", (_, _) => DeleteArchive()));
        panel.Controls.Add(buttons);
        _sections = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        _sections.SelectedIndexChanged += (_, _) => { if (!_loading) ShowSection(); };
        panel.Controls.Add(_sections);
        var sectionButtons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        sectionButtons.Controls.Add(MakeButton("新增大题", (_, _) => AddSection()));
        sectionButtons.Controls.Add(MakeButton("删除大题", (_, _) => DeleteSection()));
        panel.Controls.Add(sectionButtons);
        _stats = new Label { AutoSize = true, MaximumSize = new Size(260, 0) };
        panel.Controls.Add(_stats);
        return panel;
    }

    Control BuildEditor()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(BuildSectionFields());
        panel.Controls.Add(BuildQuestions());
        panel.Controls.Add(BuildBroadcast());
        panel.Controls.Add(BuildSettings());
        panel.Controls.Add(BuildTransport());
        return panel;
    }

    Control BuildSectionFields()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _title = Field(grid, "大题标题", 0);
        _subtitle = Field(grid, "指导语", 1);
        _title.TextChanged += (_, _) => WriteSection();
        _subtitle.TextChanged += (_, _) => WriteSection();
        return grid;
    }

    Control BuildQuestions()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _questions = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        _questions.SelectedIndexChanged += (_, _) => { if (!_loading) ShowQuestion(); };
        var qButtons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        qButtons.Controls.Add(MakeButton("单句题", (_, _) => AddQuestion(false)));
        qButtons.Controls.Add(MakeButton("对话题", (_, _) => AddQuestion(true)));
        qButtons.Controls.Add(MakeButton("删除小题", (_, _) => DeleteQuestion()));
        left.Controls.Add(_questions, 0, 0);
        left.Controls.Add(qButtons, 0, 1);
        panel.Controls.Add(left, 0, 0);
        panel.Controls.Add(BuildQuestionEditor(), 1, 0);
        return panel;
    }

    Control BuildQuestionEditor()
    {
        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, Width = 560 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _number = Field(layout, "题号", 0);
        _intro = Field(layout, "引导语", 1);
        var typePanel = new FlowLayoutPanel { AutoSize = true };
        _statementType = new RadioButton { Text = "单句朗读", AutoSize = true, Checked = true };
        _dialogueType = new RadioButton { Text = "角色对话", AutoSize = true };
        _statementType.CheckedChanged += (_, _) => ChangeType();
        typePanel.Controls.Add(_statementType);
        typePanel.Controls.Add(_dialogueType);
        layout.Controls.Add(new Label { Text = "题型", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(typePanel, 1, 2);
        _statementPanel = new Panel { Height = 90, Dock = DockStyle.Top };
        _statement = new TextBox { Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
        _statement.TextChanged += (_, _) => WriteQuestion();
        _statementPanel.Controls.Add(_statement);
        _dialoguePanel = new Panel { Height = 160, Dock = DockStyle.Top, Visible = false };
        _lines = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        _lines.Columns.Add("speaker", "说话人");
        _lines.Columns.Add("text", "台词");
        var gender = new DataGridViewComboBoxColumn { Name = "gender", HeaderText = "声音", DataSource = new[] { "男声", "女声" } };
        _lines.Columns.Add(gender);
        _lines.Columns[0].FillWeight = 20;
        _lines.Columns[1].FillWeight = 60;
        _lines.Columns[2].FillWeight = 20;
        _lines.CellValueChanged += (_, _) => WriteQuestion();
        _lines.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_lines.IsCurrentCellDirty) _lines.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        var lineButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 32 };
        lineButtons.Controls.Add(MakeButton("增加台词", (_, _) => AddLine()));
        lineButtons.Controls.Add(MakeButton("删除台词", (_, _) => DeleteLine()));
        _dialoguePanel.Controls.Add(_lines);
        _dialoguePanel.Controls.Add(lineButtons);
        var stack = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        stack.Controls.Add(layout);
        stack.Controls.Add(_statementPanel);
        stack.Controls.Add(_dialoguePanel);
        panel.Controls.Add(stack);
        _number.TextChanged += (_, _) => WriteQuestion();
        _intro.TextChanged += (_, _) => WriteQuestion();
        return panel;
    }

    Control BuildBroadcast()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _header = Field(grid, "开场白", 0);
        _footer = Field(grid, "结束语", 1);
        _header.TextChanged += (_, _) => WriteBroadcast();
        _footer.TextChanged += (_, _) => WriteBroadcast();
        return grid;
    }

    Control BuildSettings()
    {
        var group = new GroupBox { Text = "朗读设置", Dock = DockStyle.Top, Height = 230 };
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6 };
        for (int i = 0; i < 6; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16));
        _speed = new TrackBar { Minimum = 50, Maximum = 150, TickFrequency = 10, Value = 90, Dock = DockStyle.Fill };
        _speedLabel = new Label { Text = "0.90x", AutoSize = true, Anchor = AnchorStyles.Left };
        _speed.Scroll += (_, _) =>
        {
            if (_loading) return;
            CurrentSettings().speed = _speed.Value / 100.0;
            _speedLabel.Text = CurrentSettings().speed.ToString("0.00") + "x";
            SaveStore();
        };
        grid.Controls.Add(new Label { Text = "语速", AutoSize = true }, 0, 0);
        grid.Controls.Add(_speed, 1, 0);
        grid.SetColumnSpan(_speed, 3);
        grid.Controls.Add(_speedLabel, 4, 0);
        var slow = MakeButton("0.85x", (_, _) => SetSpeed(0.85));
        grid.Controls.Add(slow, 5, 0);
        _pauseLine = PauseBox(grid, "单句间隔", 0);
        _pauseDialogue = PauseBox(grid, "对话间隔", 2);
        _pauseQuestion = PauseBox(grid, "答题留白", 4);
        _chime = new CheckBox { Text = "每题前叮咚", AutoSize = true, Checked = true };
        _chime.CheckedChanged += (_, _) => { if (!_loading) { CurrentSettings().playChime = _chime.Checked; SaveStore(); } };
        grid.Controls.Add(_chime, 0, 2);
        grid.SetColumnSpan(_chime, 6);
        _male = VoiceBox(grid, "男声", 3, _voices.EnglishNames());
        _female = VoiceBox(grid, "女声", 4, _voices.EnglishNames());
        _narrator = VoiceBox(grid, "旁白", 5, _voices.NarratorNames());
        group.Controls.Add(grid);
        return group;
    }

    Control BuildTransport()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        buttons.Controls.Add(MakeButton("试听此题", async (_, _) => await PlayQuestionAsync()));
        buttons.Controls.Add(MakeButton("播放整卷", async (_, _) => await PlayAllAsync(false)));
        _pauseButton = MakeButton("暂停", (_, _) => TogglePause());
        buttons.Controls.Add(_pauseButton);
        buttons.Controls.Add(MakeButton("停止", (_, _) => StopPlayback(true)));
        buttons.Controls.Add(MakeButton("导出 WAV…", async (_, _) => await ExportAsync()));
        buttons.Controls.Add(MakeButton("恢复默认", (_, _) => ResetDefaults()));
        _progress = new ProgressBar { Dock = DockStyle.Top, Height = 16 };
        _status = new Label { AutoSize = true, Text = "就绪" };
        _dataPath = new Label { AutoSize = true, MaximumSize = new Size(800, 0) };
        panel.Controls.Add(buttons);
        panel.Controls.Add(_progress);
        panel.Controls.Add(_status);
        panel.Controls.Add(_dataPath);
        return panel;
    }

    TextBox Field(TableLayoutPanel grid, string label, int row)
    {
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        var box = new TextBox { Dock = DockStyle.Fill };
        grid.Controls.Add(box, 1, row);
        return box;
    }

    TextBox PauseBox(TableLayoutPanel grid, string label, int column)
    {
        var box = new TextBox { Dock = DockStyle.Fill };
        box.Leave += (_, _) => WritePauses();
        var stack = new Panel { Dock = DockStyle.Fill, Height = 40 };
        stack.Controls.Add(box);
        box.Dock = DockStyle.Bottom;
        stack.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, AutoSize = true });
        grid.Controls.Add(stack, column, 1);
        grid.SetColumnSpan(stack, 2);
        return box;
    }

    ComboBox VoiceBox(TableLayoutPanel grid, string label, int row, IEnumerable<string> names)
    {
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        var box = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 520 };
        foreach (string name in names) box.Items.Add(name);
        box.SelectedIndexChanged += (_, _) => WriteVoices();
        grid.Controls.Add(box, 1, row);
        grid.SetColumnSpan(box, 5);
        return box;
    }

    static Button MakeButton(string text, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true, Margin = new Padding(0, 0, 6, 6) };
        button.Click += click;
        return button;
    }

    void OpenStore()
    {
        _store.Load();
        _dataPath.Text = "题目和设置保存在本机：" + AppPaths.DataFile;
        ShowSettings();
        RefreshArchives();
    }

    void SaveStore()
    {
        if (_loading) return;
        SyncHeaderToArchive();
        try { _store.Save(); }
        catch (Exception ex) { _status.Text = "保存失败：" + ex.Message; }
    }

    void RefreshArchives()
    {
        _loading = true;
        _archives.Items.Clear();
        foreach (Archive archive in _store.Data.archives)
            _archives.Items.Add(archive.name);
        Archive current = _store.Current();
        _archives.SelectedIndex = Math.Max(0, _store.Data.archives.IndexOf(current));
        _loading = false;
        ShowArchive();
    }

    void SwitchArchive()
    {
        int index = _archives.SelectedIndex;
        if (index < 0 || index >= _store.Data.archives.Count) return;
        _store.Data.current_archive_id = _store.Data.archives[index].id;
        SaveStore();
        ShowArchive();
    }

    void ShowArchive()
    {
        Archive archive = _store.Current();
        _loading = true;
        _header.Text = archive.examHeader ?? "";
        _footer.Text = string.IsNullOrEmpty(archive.examFooter) ? VoiceDefaults.Footer : archive.examFooter;
        _loading = false;
        RefreshSections();
        UpdateStats();
    }

    void RefreshSections()
    {
        int selected = _sections.SelectedIndex;
        _loading = true;
        _sections.Items.Clear();
        foreach (Section section in _store.Current().sections)
        {
            string title = string.IsNullOrWhiteSpace(section.title) ? "（未命名大题）" : section.title;
            _sections.Items.Add(title + (string.IsNullOrWhiteSpace(section.subtitle) ? "" : "  " + section.subtitle));
        }
        _loading = false;
        if (_sections.Items.Count == 0)
        {
            ClearQuestionEditor();
            return;
        }
        _sections.SelectedIndex = Math.Min(Math.Max(selected, 0), _sections.Items.Count - 1);
        ShowSection();
    }

    Section SelectedSection()
    {
        int index = _sections.SelectedIndex;
        var sections = _store.Current().sections;
        return index >= 0 && index < sections.Count ? sections[index] : null;
    }

    void ShowSection()
    {
        Section section = SelectedSection();
        _loading = true;
        _title.Text = section?.title ?? "";
        _subtitle.Text = section?.subtitle ?? "";
        _loading = false;
        RefreshQuestions();
    }

    void WriteSection()
    {
        if (_loading) return;
        Section section = SelectedSection();
        if (section == null) return;
        section.title = _title.Text;
        section.subtitle = _subtitle.Text;
        int index = _sections.SelectedIndex;
        _loading = true;
        if (index >= 0)
        {
            string title = string.IsNullOrWhiteSpace(section.title) ? "（未命名大题）" : section.title;
            _sections.Items[index] = title + (string.IsNullOrWhiteSpace(section.subtitle) ? "" : "  " + section.subtitle);
        }
        _loading = false;
        SaveStore();
    }

    void RefreshQuestions()
    {
        int selected = _questions.SelectedIndex;
        _loading = true;
        _questions.Items.Clear();
        Section section = SelectedSection();
        if (section != null)
        {
            foreach (Question question in section.items)
            {
                string kind = question.type == "dialogue" ? "对话" : "单句";
                _questions.Items.Add("No. " + question.num + "  " + kind);
            }
        }
        _loading = false;
        if (_questions.Items.Count == 0)
        {
            ClearQuestionEditor();
            UpdateStats();
            return;
        }
        _questions.SelectedIndex = Math.Min(Math.Max(selected, 0), _questions.Items.Count - 1);
        ShowQuestion();
        UpdateStats();
    }

    Question SelectedQuestion()
    {
        Section section = SelectedSection();
        if (section == null) return null;
        int index = _questions.SelectedIndex;
        return index >= 0 && index < section.items.Count ? section.items[index] : null;
    }

    void ShowQuestion()
    {
        Question question = SelectedQuestion();
        _loading = true;
        _number.Text = question?.num ?? "";
        _intro.Text = question?.introText ?? "";
        bool dialogue = question != null && question.type == "dialogue";
        _dialogueType.Checked = dialogue;
        _statementType.Checked = !dialogue;
        _statement.Text = question?.text ?? "";
        _statementPanel.Visible = !dialogue;
        _dialoguePanel.Visible = dialogue;
        _lines.Rows.Clear();
        if (dialogue && question.dialogue != null)
        {
            foreach (Line line in question.dialogue)
                _lines.Rows.Add(line.speaker, line.text, line.gender == "female" ? "女声" : "男声");
        }
        _loading = false;
    }

    void ClearQuestionEditor()
    {
        _loading = true;
        _number.Text = "";
        _intro.Text = "";
        _statement.Text = "";
        _lines.Rows.Clear();
        _loading = false;
    }

    void WriteQuestion()
    {
        if (_loading) return;
        Question question = SelectedQuestion();
        if (question == null) return;
        question.num = _number.Text.Trim();
        question.introText = _intro.Text;
        question.type = _dialogueType.Checked ? "dialogue" : "statement";
        if (question.type == "statement")
        {
            question.text = _statement.Text;
            question.dialogue = null;
        }
        else
        {
            question.text = null;
            question.dialogue = new List<Line>();
            foreach (DataGridViewRow row in _lines.Rows)
            {
                if (row.IsNewRow) continue;
                question.dialogue.Add(new Line
                {
                    id = "dl-" + Ids.Now() + "-" + question.dialogue.Count,
                    speaker = Convert.ToString(row.Cells[0].Value) ?? "A",
                    text = Convert.ToString(row.Cells[1].Value) ?? "",
                    gender = Convert.ToString(row.Cells[2].Value) == "女声" ? "female" : "male"
                });
            }
        }
        int index = _questions.SelectedIndex;
        if (index >= 0)
            _questions.Items[index] = "No. " + question.num + "  " + (question.type == "dialogue" ? "对话" : "单句");
        SaveStore();
        UpdateStats();
    }

    void ChangeType()
    {
        if (_loading) return;
        Question question = SelectedQuestion();
        if (question == null) return;
        bool dialogue = _dialogueType.Checked;
        if (dialogue && question.type != "dialogue")
        {
            string kept = question.text ?? "";
            question.type = "dialogue";
            question.text = null;
            string stamp = Ids.Now();
            question.dialogue = new List<Line>
            {
                new Line { id = "dl-" + stamp + "-1", speaker = "A", text = kept, gender = "male" },
                new Line { id = "dl-" + stamp + "-2", speaker = "B", text = "", gender = "female" }
            };
        }
        else if (!dialogue && question.type != "statement")
        {
            string kept = question.dialogue != null && question.dialogue.Count > 0 ? question.dialogue[0].text : "";
            question.type = "statement";
            question.text = kept ?? "";
            question.dialogue = null;
        }
        ShowQuestion();
        SaveStore();
    }

    void WriteBroadcast()
    {
        if (_loading) return;
        Archive archive = _store.Current();
        archive.examHeader = _header.Text;
        archive.examFooter = _footer.Text;
        SyncHeaderToArchive();
        SaveStore();
    }

    void SyncHeaderToArchive()
    {
        Archive archive = _store.Current();
        _store.Data.config.examHeader = archive.examHeader ?? "";
        _store.Data.config.examFooter = archive.examFooter ?? "";
    }

    void WritePauses()
    {
        if (_loading) return;
        Settings settings = CurrentSettings();
        settings.pauseAfterLine = ReadPause(_pauseLine, 1.5);
        settings.pauseAfterDialogueLine = ReadPause(_pauseDialogue, 0.1);
        settings.pauseAfterQuestion = ReadPause(_pauseQuestion, 4);
        SaveStore();
    }

    static double ReadPause(TextBox box, double fallback)
    {
        return double.TryParse(box.Text.Trim(), out double value) && value >= 0 ? value : fallback;
    }

    void WriteVoices()
    {
        if (_loading) return;
        Settings settings = CurrentSettings();
        settings.browserMaleVoice = Convert.ToString(_male.SelectedItem) ?? VoiceDefaults.Male;
        settings.browserFemaleVoice = Convert.ToString(_female.SelectedItem) ?? VoiceDefaults.Female;
        settings.browserChineseVoice = Convert.ToString(_narrator.SelectedItem) ?? VoiceDefaults.Chinese;
        SaveStore();
    }

    void ShowSettings()
    {
        Settings settings = CurrentSettings();
        _loading = true;
        _speed.Value = Math.Min(_speed.Maximum, Math.Max(_speed.Minimum, (int)Math.Round(settings.speed * 100)));
        _speedLabel.Text = settings.speed.ToString("0.00") + "x";
        _pauseLine.Text = settings.pauseAfterLine.ToString("0.##");
        _pauseDialogue.Text = settings.pauseAfterDialogueLine.ToString("0.##");
        _pauseQuestion.Text = settings.pauseAfterQuestion.ToString("0.##");
        _chime.Checked = settings.playChime;
        SelectVoice(_male, settings.browserMaleVoice, VoiceDefaults.Male);
        SelectVoice(_female, settings.browserFemaleVoice, VoiceDefaults.Female);
        SelectVoice(_narrator, settings.browserChineseVoice, VoiceDefaults.Chinese);
        _loading = false;
    }

    static void SelectVoice(ComboBox box, string name, string fallback)
    {
        int index = box.Items.IndexOf(name);
        if (index < 0) index = box.Items.IndexOf(fallback);
        box.SelectedIndex = index >= 0 ? index : (box.Items.Count > 0 ? 0 : -1);
    }

    void SetSpeed(double speed)
    {
        CurrentSettings().speed = speed;
        ShowSettings();
        SaveStore();
    }

    Settings CurrentSettings() => _store.Data.config;

    void CreateArchive()
    {
        string name = Ask("新建听力材料", "");
        if (string.IsNullOrWhiteSpace(name)) return;
        Archive archive = Archive.Create(name.Trim());
        _store.Data.archives.Add(archive);
        _store.Data.current_archive_id = archive.id;
        SaveStore();
        RefreshArchives();
    }

    void RenameArchive()
    {
        Archive archive = _store.Current();
        string name = Ask("重命名材料", archive.name);
        if (string.IsNullOrWhiteSpace(name)) return;
        archive.name = name.Trim();
        SaveStore();
        RefreshArchives();
    }

    void DeleteArchive()
    {
        if (_store.Data.archives.Count <= 1)
        {
            MessageBox.Show(this, "至少保留一份听力材料。", "无法删除");
            return;
        }
        Archive archive = _store.Current();
        if (MessageBox.Show(this, "确定删除「" + archive.name + "」吗？", "删除材料", MessageBoxButtons.OKCancel) != DialogResult.OK)
            return;
        _store.Data.archives.Remove(archive);
        _store.Data.current_archive_id = _store.Data.archives[0].id;
        SaveStore();
        RefreshArchives();
    }

    void AddSection()
    {
        var section = Section.Create();
        section.items.Add(Question.Statement(NextNumber()));
        _store.Current().sections.Add(section);
        SaveStore();
        RefreshSections();
        _sections.SelectedIndex = _sections.Items.Count - 1;
    }

    void DeleteSection()
    {
        Section section = SelectedSection();
        if (section == null) return;
        if (MessageBox.Show(this, "确定删除这个大题吗？", "删除大题", MessageBoxButtons.OKCancel) != DialogResult.OK)
            return;
        _store.Current().sections.Remove(section);
        SaveStore();
        RefreshSections();
    }

    void AddQuestion(bool dialogue)
    {
        Section section = SelectedSection();
        if (section == null)
        {
            AddSection();
            section = SelectedSection();
            if (section == null) return;
            if (section.items.Count == 1 && !dialogue) return;
        }
        string number = NextNumber();
        section.items.Add(dialogue ? Question.Dialogue(number) : Question.Statement(number));
        SaveStore();
        RefreshQuestions();
        _questions.SelectedIndex = _questions.Items.Count - 1;
    }

    void DeleteQuestion()
    {
        Section section = SelectedSection();
        Question question = SelectedQuestion();
        if (section == null || question == null) return;
        section.items.Remove(question);
        SaveStore();
        RefreshQuestions();
    }

    void AddLine()
    {
        if (SelectedQuestion() == null || !_dialogueType.Checked) return;
        string previous = _lines.Rows.Count == 0 ? "女声" : Convert.ToString(_lines.Rows[_lines.Rows.Count - 1].Cells[2].Value);
        string speaker = _lines.Rows.Count == 0 || Convert.ToString(_lines.Rows[_lines.Rows.Count - 1].Cells[0].Value) != "A" ? "A" : "B";
        _lines.Rows.Add(speaker, "", previous == "男声" ? "女声" : "男声");
        WriteQuestion();
    }

    void DeleteLine()
    {
        if (_lines.CurrentRow == null) return;
        _lines.Rows.Remove(_lines.CurrentRow);
        WriteQuestion();
    }

    string NextNumber()
    {
        int max = 0;
        foreach (Section section in _store.Current().sections)
        {
            foreach (Question question in section.items)
            {
                string raw = question.num ?? "";
                var match = System.Text.RegularExpressions.Regex.Match(raw, @"-(\d+)");
                string digits = match.Success ? match.Groups[1].Value : raw;
                if (int.TryParse(digits, out int value) && value > max) max = value;
            }
        }
        return (max + 1).ToString();
    }

    void UpdateStats()
    {
        Archive archive = _store.Current();
        int questions = archive.sections.Sum(section => section.items.Count);
        int words = 0;
        foreach (Segment segment in Timeline.FullExam(archive, CurrentSettings(), _voices))
        {
            if (segment.Kind != SegmentKind.Speech) continue;
            words += System.Text.RegularExpressions.Regex.Matches(segment.Text, "[A-Za-z0-9']+").Count;
            foreach (char character in segment.Text)
                if (character >= 0x4e00 && character <= 0x9fff) words++;
        }
        _stats.Text = "大题 " + archive.sections.Count + "    小题 " + questions + "    朗读词数 " + words;
    }

    async Task PlayQuestionAsync()
    {
        Question question = SelectedQuestion();
        if (question == null)
        {
            _status.Text = "先选择一道小题。";
            return;
        }
        await RunPlaybackAsync(Timeline.OneQuestion(question, CurrentSettings(), _voices), -1, -1);
    }

    async Task PlayAllAsync(bool resume)
    {
        var segments = new List<Segment>();
        Archive archive = _store.Current();
        int sectionIndex = resume ? Math.Max(0, _resumeSection) : 0;
        int questionIndex = resume ? Math.Max(0, _resumeQuestion) : 0;
        if (!resume || (sectionIndex == 0 && questionIndex == 0))
        {
            var all = Timeline.FullExam(archive, CurrentSettings(), _voices);
            await RunPlaybackAsync(all, -1, -1);
            return;
        }
        Section previous = sectionIndex > 0 ? archive.sections[sectionIndex - 1] : null;
        for (int s = sectionIndex; s < archive.sections.Count; s++)
        {
            Section section = archive.sections[s];
            if (questionIndex == 0)
            {
                string title = (section.title ?? "").Trim();
                string spoken = previous != null && title == (previous.title ?? "").Trim()
                    ? (section.subtitle ?? "").Trim()
                    : (title + " " + (section.subtitle ?? "")).Trim();
                if (!string.IsNullOrWhiteSpace(spoken))
                    segments.AddRange(SpeechOnly(spoken, "narrator", 2.2));
            }
            for (int q = questionIndex; q < section.items.Count; q++)
                segments.AddRange(Timeline.OneQuestion(section.items[q], CurrentSettings(), _voices));
            questionIndex = 0;
            previous = section;
        }
        if (!string.IsNullOrWhiteSpace(archive.examFooter))
            segments.AddRange(SpeechOnly(archive.examFooter, "narrator", 0));
        await RunPlaybackAsync(segments, -1, -1);
    }

    List<Segment> SpeechOnly(string text, string role, double pause)
    {
        var list = new List<Segment>();
        string display = role == "male" ? CurrentSettings().browserMaleVoice : CurrentSettings().browserFemaleVoice;
        if (role == "narrator")
            display = VoiceCatalog.ContainsChinese(text) ? CurrentSettings().browserChineseVoice : CurrentSettings().browserFemaleVoice;
        list.Add(new Segment { Kind = SegmentKind.Speech, Text = text, VoiceId = _voices.Resolve(display, role, text, CurrentSettings()), Description = "朗读" });
        if (pause > 0) list.Add(new Segment { Kind = SegmentKind.Silence, Seconds = pause });
        return list;
    }

    async Task RunPlaybackAsync(List<Segment> segments, int resumeSection, int resumeQuestion)
    {
        if (_busy) return;
        if (segments.Count == 0)
        {
            _status.Text = "没有可播放的内容。";
            return;
        }
        _busy = true;
        _play = new CancellationTokenSource();
        _resumeSection = resumeSection;
        _resumeQuestion = resumeQuestion;
        try
        {
            byte[] pcm = await RenderAsync(segments, new Progress<string>(text => _status.Text = text), _play.Token);
            _status.Text = "正在播放";
            await _player.PlayAsync(pcm, _play.Token);
            _status.Text = "播放完成";
            _resumeSection = -1;
            _resumeQuestion = -1;
        }
        catch (OperationCanceledException)
        {
            _status.Text = _resumeSection >= 0 ? "已暂停，可从当前题继续" : "已停止";
        }
        catch (Exception ex)
        {
            _status.Text = "播放失败：" + ex.Message;
            MessageBox.Show(this, ex.Message, "朗读失败");
        }
        finally
        {
            _busy = false;
            _progress.Value = 0;
        }
    }

    void TogglePause()
    {
        if (!_busy && _resumeSection >= 0)
        {
            _pauseButton.Text = "暂停";
            _ = PlayAllAsync(true);
            return;
        }
        Pause();
        _pauseButton.Text = "继续";
    }

    void Pause()
    {
        RememberPosition();
        _play?.Cancel();
        _player.Stop();
    }

    void RememberPosition()
    {
        Question question = SelectedQuestion();
        Section section = SelectedSection();
        Archive archive = _store.Current();
        _resumeSection = section == null ? 0 : archive.sections.IndexOf(section);
        _resumeQuestion = question == null || section == null ? 0 : section.items.IndexOf(question);
    }

    void StopPlayback(bool resetPosition)
    {
        if (resetPosition)
        {
            _resumeSection = -1;
            _resumeQuestion = -1;
        }
        _play?.Cancel();
        _player.Stop();
        _status.Text = "已停止";
    }

    async Task ExportAsync()
    {
        if (_busy) return;
        var segments = Timeline.FullExam(_store.Current(), CurrentSettings(), _voices);
        if (segments.Count == 0)
        {
            MessageBox.Show(this, "当前材料没有可导出的内容。", "导出");
            return;
        }
        using var dialog = new SaveFileDialog
        {
            Title = "选择整卷音频的保存位置",
            Filter = "WAV 音频|*.wav",
            FileName = SafeFileName(_store.Current().name) + ".wav",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _busy = true;
        _play = new CancellationTokenSource();
        try
        {
            byte[] pcm = await RenderAsync(segments, new Progress<string>(text => _status.Text = text), _play.Token);
            AudioMixer.WriteWav(dialog.FileName, pcm);
            _status.Text = "已保存：" + dialog.FileName;
            MessageBox.Show(this, "整卷音频已保存到：\n" + dialog.FileName, "导出完成");
        }
        catch (OperationCanceledException)
        {
            _status.Text = "已取消导出";
        }
        catch (Exception ex)
        {
            _status.Text = "导出失败：" + ex.Message;
            MessageBox.Show(this, ex.Message, "导出失败");
        }
        finally
        {
            _busy = false;
            _progress.Value = 0;
        }
    }

    async Task<byte[]> RenderAsync(List<Segment> segments, IProgress<string> progress, CancellationToken cancellationToken)
    {
        int speechCount = segments.Count(segment => segment.Kind == SegmentKind.Speech);
        int speechIndex = 0;
        var parts = new List<byte[]>();
        foreach (Segment segment in segments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (segment.Kind == SegmentKind.Chime)
            {
                progress.Report("提示音");
                parts.Add(AudioMixer.Chime());
            }
            else if (segment.Kind == SegmentKind.Silence)
            {
                parts.Add(AudioMixer.Silence(segment.Seconds));
            }
            else
            {
                speechIndex++;
                progress.Report("正在合成 " + speechIndex + "/" + speechCount + "：" + segment.Description);
                _progress.Value = speechCount == 0 ? 0 : Math.Min(100, speechIndex * 100 / speechCount);
                byte[] mp3 = await CachedSpeechAsync(segment, cancellationToken);
                parts.Add(AudioMixer.DecodeMp3(mp3));
            }
        }
        return AudioMixer.Concat(parts);
    }

    async Task<byte[]> CachedSpeechAsync(Segment segment, CancellationToken cancellationToken)
    {
        string key = segment.Text + "\n" + segment.VoiceId + "\n" + CurrentSettings().speed.ToString("0.###");
        if (_cache.TryGetValue(key, out byte[] cached)) return cached;
        byte[] audio = await _tts.SynthesizeAsync(segment.Text, segment.VoiceId, CurrentSettings().speed, cancellationToken);
        _cache[key] = audio;
        return audio;
    }

    void ResetDefaults()
    {
        if (MessageBox.Show(this, "恢复默认会清空当前材料的题目，并把语速、停顿和音色恢复为最初设置。确定继续吗？", "恢复默认", MessageBoxButtons.OKCancel) != DialogResult.OK)
            return;
        Archive archive = _store.Current();
        archive.sections.Clear();
        archive.examHeader = "";
        archive.examFooter = VoiceDefaults.Footer;
        _store.Data.config = Settings.CreateDefault();
        SaveStore();
        ShowSettings();
        ShowArchive();
        _status.Text = "已恢复默认。男声 William，女声 Natasha，旁白晓晓。";
    }

    static string Ask(string title, string initial)
    {
        using var form = new Form
        {
            Text = title,
            Width = 420,
            Height = 150,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var box = new TextBox { Left = 12, Top = 16, Width = 380, Text = initial ?? "" };
        var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Left = 216, Top = 52, Width = 80 };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Left = 304, Top = 52, Width = 80 };
        form.Controls.AddRange(new Control[] { box, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog() == DialogResult.OK ? box.Text.Trim() : null;
    }

    static string SafeFileName(string name)
    {
        string value = string.IsNullOrWhiteSpace(name) ? "听力音频" : name.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return value;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _play?.Cancel();
            _player.Dispose();
        }
        base.Dispose(disposing);
    }
}
