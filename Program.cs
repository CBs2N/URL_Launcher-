using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace UrlLauncherApp
{
    public class UrlEntry
    {
        public string Title;
        public string Url;
    }

    public class LinkRecord
    {
        public int order;
        public string title;
        public string url;
    }

    public class LinkStore
    {
        public int version;
        public List<LinkRecord> links;
    }

    public class AppSettings
    {
        public string browserName;
        public string browserPath;
        public double openIntervalSeconds;
    }

    public class BrowserInfo
    {
        public string Name;
        public string Path;
        public override string ToString()
        {
            return Name + " (" + Path + ")";
        }
    }

    public class ShadowButton : Button
    {
        public ShadowButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 1;
            FlatAppearance.BorderColor = Color.FromArgb(176, 206, 235);
            FlatAppearance.MouseOverBackColor = Color.FromArgb(236, 247, 255);
            FlatAppearance.MouseDownBackColor = Color.FromArgb(226, 242, 255);
            BackColor = Color.FromArgb(245, 251, 255);
            ForeColor = Color.FromArgb(36, 54, 82);
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            Cursor = Cursors.Hand;
            TextAlign = ContentAlignment.MiddleCenter;
            Padding = new Padding(0);
            AutoSize = false;
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private sealed class UrlParseResult
        {
            public string Url;
            public string ExtractedTitle;
        }

        private readonly TextBox _urlInput = new TextBox();
        private readonly TextBox _searchInput = new TextBox();
        private readonly ListView _urlList = new ListView();
        private readonly Label _fileLabel = new Label();
        private readonly List<UrlEntry> _items = new List<UrlEntry>();
        private readonly List<UrlEntry> _filteredItems = new List<UrlEntry>();
        private readonly ContextMenuStrip _listMenu = new ContextMenuStrip();
        private string _currentFile;
        private readonly string _defaultFile;
        private readonly string _settingsFile;
        private bool _hasUnsavedChanges;
        private AppSettings _appSettings;
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public MainForm()
        {
            Text = "网址批量打开工具";
            Width = 1200;
            Height = 620;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1080, 540);
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            DoubleBuffered = true;

            // 加载应用图标（窗口标题栏+任务栏）
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(iconPath)) Icon = new Icon(iconPath);

            _defaultFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "saved_urls.json");
            _settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_settings.json");
            _currentFile = _defaultFile;
            _appSettings = LoadAppSettings();

            BuildUi();
            LoadStartupUrls();
            FormClosing += MainForm_FormClosing;
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(12),
                BackColor = Color.Transparent
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            Controls.Add(root);

            var title = new Label
            {
                Text = "网址批量打开工具",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold),
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(30, 62, 98)
            };
            root.Controls.Add(title, 0, 0);

            var inputPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                BackColor = Color.Transparent
            };
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            root.Controls.Add(inputPanel, 0, 1);

            var inputLabel = new Label
            {
                Text = "输入网址：",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(46, 74, 110),
                AutoSize = false
            };
            inputPanel.Controls.Add(inputLabel, 0, 0);

            _urlInput.Dock = DockStyle.Fill;
            _urlInput.Font = new Font("Segoe UI", 10F);
            _urlInput.BorderStyle = BorderStyle.FixedSingle;
            _urlInput.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    AddUrl();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            inputPanel.Controls.Add(_urlInput, 1, 0);

            var addButton = new ShadowButton { Text = "添加", Dock = DockStyle.Fill, Height = 32 };
            addButton.Click += (s, e) => AddUrl();
            inputPanel.Controls.Add(addButton, 2, 0);

            var searchPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                BackColor = Color.Transparent
            };
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(searchPanel, 0, 2);

            var searchLabel = new Label
            {
                Text = "搜索标题：",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(46, 74, 110),
                AutoSize = false
            };
            searchPanel.Controls.Add(searchLabel, 0, 0);

            _searchInput.Dock = DockStyle.Fill;
            _searchInput.Font = new Font("Segoe UI", 10F);
            _searchInput.BorderStyle = BorderStyle.FixedSingle;
            _searchInput.TextChanged += (s, e) => RefreshListView();
            searchPanel.Controls.Add(_searchInput, 1, 0);

            _urlList.Dock = DockStyle.Fill;
            _urlList.Font = new Font("Segoe UI", 9.5F);
            _urlList.View = View.Details;
            _urlList.FullRowSelect = true;
            _urlList.MultiSelect = false;
            _urlList.HideSelection = false;
            _urlList.BackColor = Color.FromArgb(252, 254, 255);
            _urlList.ForeColor = Color.FromArgb(38, 60, 92);
            _urlList.GridLines = true;
            _urlList.Columns.Add("序号", 70);
            _urlList.Columns.Add("标题", 250);
            _urlList.Columns.Add("网址", 640);
            _urlList.MouseUp += UrlList_MouseUp;
            root.Controls.Add(_urlList, 0, 3);

            var editTitleMenuItem = new ToolStripMenuItem("编辑标题");
            editTitleMenuItem.Click += (s, e) => EditSelectedTitle();
            _listMenu.Items.Add(editTitleMenuItem);
            _urlList.ContextMenuStrip = _listMenu;

            var actionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent
            };
            root.Controls.Add(actionPanel, 0, 4);

            var delButton = new ShadowButton { Text = "删除选中", Width = 90, Height = 32 };
            delButton.Click += (s, e) => RemoveSelected();
            actionPanel.Controls.Add(delButton);

            var clearButton = new ShadowButton { Text = "清空全部", Width = 90, Height = 32 };
            clearButton.Click += (s, e) => ClearAll();
            actionPanel.Controls.Add(clearButton);

            var moveUpButton = new ShadowButton { Text = "上移", Width = 70, Height = 32 };
            moveUpButton.Click += (s, e) => MoveSelected(-1);
            actionPanel.Controls.Add(moveUpButton);

            var moveDownButton = new ShadowButton { Text = "下移", Width = 70, Height = 32 };
            moveDownButton.Click += (s, e) => MoveSelected(1);
            actionPanel.Controls.Add(moveDownButton);

            var sortAscButton = new ShadowButton { Text = "排序升序", Width = 90, Height = 32 };
            sortAscButton.Click += (s, e) => SortUrls(true);
            actionPanel.Controls.Add(sortAscButton);

            var sortDescButton = new ShadowButton { Text = "排序降序", Width = 90, Height = 32 };
            sortDescButton.Click += (s, e) => SortUrls(false);
            actionPanel.Controls.Add(sortDescButton);

            var saveButton = new ShadowButton { Text = "保存", Width = 90, Height = 32 };
            saveButton.Click += (s, e) => SaveUrls();
            actionPanel.Controls.Add(saveButton);

            var chooseFileButton = new ShadowButton { Text = "选择预设文件", Width = 132, Height = 32 };
            chooseFileButton.Click += (s, e) => ChooseFile();
            actionPanel.Controls.Add(chooseFileButton);

            var openAllButton = new ShadowButton { Text = "一键按顺序打开", Width = 148, Height = 32 };
            openAllButton.Click += async (s, e) => await OpenAllUrlsAsync();
            actionPanel.Controls.Add(openAllButton);

            var openPartialButton = new ShadowButton { Text = "选择部分打开", Width = 132, Height = 32 };
            openPartialButton.Click += async (s, e) => await OpenPartialUrlsAsync();
            actionPanel.Controls.Add(openPartialButton);

            var settingsButton = new ShadowButton { Text = "设置", Width = 80, Height = 32 };
            settingsButton.Click += (s, e) => OpenSettings();
            actionPanel.Controls.Add(settingsButton);

            _fileLabel.Dock = DockStyle.Fill;
            _fileLabel.TextAlign = ContentAlignment.MiddleLeft;
            _fileLabel.BackColor = Color.Transparent;
            _fileLabel.ForeColor = Color.FromArgb(52, 82, 118);
            root.Controls.Add(_fileLabel, 0, 5);
            UpdateFileLabel();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var brush = new LinearGradientBrush(
                ClientRectangle,
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(223, 241, 255),
                LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }

        private static string TrimSingleOuterBracket(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length < 2) return text;
            var pairs = new Dictionary<char, char>
            {
                {'(', ')'}, {'（', '）'}, {'[', ']'}, {'［', '］'}, {'{', '}'}, {'｛', '｝'},
                {'<', '>'}, {'〈', '〉'}, {'《', '》'}, {'【', '】'}, {'〖', '〗'}, {'〔', '〕'}, {'「', '」'}
            };
            var first = text[0];
            char last;
            if (!pairs.TryGetValue(first, out last)) return text;
            if (text[text.Length - 1] == last)
            {
                return text.Substring(1, text.Length - 2).Trim();
            }
            return text;
        }

        private static UrlParseResult ParseInput(string raw)
        {
            var result = new UrlParseResult();
            var text = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text)) return result;

            var lower = text.ToLowerInvariant();
            var httpIdx = lower.IndexOf("http://");
            var httpsIdx = lower.IndexOf("https://");
            var idx = -1;
            if (httpIdx >= 0 && httpsIdx >= 0) idx = Math.Min(httpIdx, httpsIdx);
            else if (httpIdx >= 0) idx = httpIdx;
            else if (httpsIdx >= 0) idx = httpsIdx;

            string urlPart;
            string titlePart = string.Empty;
            if (idx >= 0)
            {
                titlePart = text.Substring(0, idx).Trim();
                titlePart = TrimSingleOuterBracket(titlePart);
                urlPart = text.Substring(idx).Trim();
            }
            else
            {
                urlPart = text;
            }

            if (!urlPart.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !urlPart.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                urlPart = "https://" + urlPart;
            }

            result.Url = urlPart;
            result.ExtractedTitle = titlePart;
            return result;
        }

        private void LoadStartupUrls()
        {
            if (!File.Exists(_defaultFile))
            {
                var legacyTxt = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "saved_urls.txt");
                if (File.Exists(legacyTxt))
                {
                    LoadFromFile(legacyTxt);
                    _currentFile = _defaultFile;
                    SaveUrls();
                }
                else
                {
                    _items.Clear();
                    _items.Add(new UrlEntry { Title = "百度", Url = "https://www.baidu.com" });
                    _items.Add(new UrlEntry { Title = "必应", Url = "https://www.bing.com" });
                    SaveUrls();
                }
            }
            LoadFromFile(_defaultFile);
        }

        private void LoadFromFile(string filePath)
        {
            _items.Clear();
            if (File.Exists(filePath))
            {
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext == ".json")
                {
                    try
                    {
                        var json = File.ReadAllText(filePath);
                        var store = Json.Deserialize<LinkStore>(json);
                        if (store != null && store.links != null)
                        {
                            var ordered = store.links.OrderBy(x => x.order).ToList();
                            foreach (var rec in ordered)
                            {
                                var parsed = ParseInput(rec.url);
                                if (string.IsNullOrWhiteSpace(parsed.Url)) continue;
                                _items.Add(new UrlEntry
                                {
                                    Title = (rec.title ?? string.Empty).Trim(),
                                    Url = parsed.Url
                                });
                            }
                        }
                    }
                    catch
                    {
                        MessageBox.Show("JSON 文件格式无法解析。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    foreach (var line in File.ReadAllLines(filePath))
                    {
                        var text = (line ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(text)) continue;
                        string title = string.Empty;
                        string url = text;

                        var parts = text.Split(new[] { '\t' }, 2);
                        if (parts.Length == 2)
                        {
                            title = parts[0].Trim();
                            url = parts[1].Trim();
                        }

                        var parsed = ParseInput(url);
                        if (string.IsNullOrWhiteSpace(parsed.Url)) continue;
                        _items.Add(new UrlEntry { Title = title, Url = parsed.Url });
                    }
                }
            }
            _currentFile = filePath;
            UpdateFileLabel();
            RefreshListView();
            _hasUnsavedChanges = false;
            UpdateWindowTitle();
        }

        private void RefreshListView()
        {
            _urlList.Items.Clear();
            _filteredItems.Clear();

            var keyword = (_searchInput.Text ?? string.Empty).Trim();
            IEnumerable<UrlEntry> source = _items;
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                source = _items.Where(x => (x.Title ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            foreach (var item in source)
            {
                _filteredItems.Add(item);
            }

            for (var i = 0; i < _filteredItems.Count; i++)
            {
                var item = _filteredItems[i];
                var row = new ListViewItem((i + 1).ToString());
                row.SubItems.Add(item.Title ?? string.Empty);
                row.SubItems.Add(item.Url ?? string.Empty);
                _urlList.Items.Add(row);
            }
        }

        private static string PromptForTitle(string initialTitle)
        {
            using (var form = new Form())
            {
                form.Text = "备注标题";
                // 加载应用图标
                var titleIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(titleIconPath)) form.Icon = new Icon(titleIconPath);
                form.StartPosition = FormStartPosition.CenterParent;
                form.Width = 460;
                form.Height = 180;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                var label = new Label
                {
                    Text = "请输入该网址的标题（可跳过）：",
                    Left = 12,
                    Top = 15,
                    Width = 420
                };
                var input = new TextBox
                {
                    Left = 12,
                    Top = 45,
                    Width = 420,
                    Text = initialTitle ?? string.Empty
                };
                var okButton = new Button { Text = "确定", Left = 192, Width = 75, Top = 82, DialogResult = DialogResult.OK };
                var skipButton = new Button { Text = "跳过", Left = 277, Width = 75, Top = 82, DialogResult = DialogResult.Ignore };
                var cancelButton = new Button { Text = "取消", Left = 357, Width = 75, Top = 82, DialogResult = DialogResult.Cancel };

                form.Controls.Add(label);
                form.Controls.Add(input);
                form.Controls.Add(okButton);
                form.Controls.Add(skipButton);
                form.Controls.Add(cancelButton);
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                var dr = form.ShowDialog();
                if (dr == DialogResult.OK) return (input.Text ?? string.Empty).Trim();
                if (dr == DialogResult.Ignore) return string.Empty;
                return null;
            }
        }

        private void AddUrl()
        {
            var parsed = ParseInput(_urlInput.Text);
            if (string.IsNullOrWhiteSpace(parsed.Url)) return;

            if (_items.Any(x => string.Equals(x.Url, parsed.Url, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("该网址已存在。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var title = PromptForTitle(parsed.ExtractedTitle);
            if (title == null) return;

            _items.Add(new UrlEntry { Title = title, Url = parsed.Url });
            _urlInput.Clear();
            RefreshListView();
            MarkUnsaved();
        }

        private void RemoveSelected()
        {
            if (_urlList.SelectedIndices.Count == 0) return;
            var viewIndex = _urlList.SelectedIndices[0];
            if (viewIndex < 0 || viewIndex >= _filteredItems.Count) return;
            var target = _filteredItems[viewIndex];
            _items.Remove(target);
            RefreshListView();
            MarkUnsaved();
        }

        private void MoveSelected(int offset)
        {
            if (_urlList.SelectedIndices.Count == 0) return;
            var viewIndex = _urlList.SelectedIndices[0];
            if (viewIndex < 0 || viewIndex >= _filteredItems.Count) return;
            var selected = _filteredItems[viewIndex];
            var indexInAll = _items.IndexOf(selected);
            if (indexInAll < 0) return;
            var targetIndex = indexInAll + offset;
            if (targetIndex < 0 || targetIndex >= _items.Count) return;

            _items.RemoveAt(indexInAll);
            _items.Insert(targetIndex, selected);
            RefreshListView();

            var newViewIndex = _filteredItems.IndexOf(selected);
            if (newViewIndex >= 0 && newViewIndex < _urlList.Items.Count)
            {
                _urlList.Items[newViewIndex].Selected = true;
            }
            MarkUnsaved();
        }

        private void ClearAll()
        {
            if (_items.Count == 0) return;
            var res = MessageBox.Show("确定要清空全部网址吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res == DialogResult.Yes)
            {
                _items.Clear();
                RefreshListView();
                MarkUnsaved();
            }
        }

        private void SortUrls(bool ascending)
        {
            var list = ascending
                ? _items.OrderBy(x => x.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Url, StringComparer.OrdinalIgnoreCase).ToList()
                : _items.OrderByDescending(x => x.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenByDescending(x => x.Url, StringComparer.OrdinalIgnoreCase).ToList();
            _items.Clear();
            _items.AddRange(list);
            RefreshListView();
            MarkUnsaved();
        }

        private void SaveUrls()
        {
            try
            {
                var normalizedFile = _currentFile;
                if (!string.Equals(Path.GetExtension(normalizedFile), ".json", StringComparison.OrdinalIgnoreCase))
                {
                    normalizedFile = Path.ChangeExtension(normalizedFile, ".json");
                    _currentFile = normalizedFile;
                    UpdateFileLabel();
                }

                var store = new LinkStore();
                store.version = 1;
                store.links = new List<LinkRecord>();
                for (var i = 0; i < _items.Count; i++)
                {
                    store.links.Add(new LinkRecord
                    {
                        order = i + 1,
                        title = (_items[i].Title ?? string.Empty).Trim(),
                        url = (_items[i].Url ?? string.Empty).Trim()
                    });
                }

                File.WriteAllText(normalizedFile, PrettyJson(store), Encoding.UTF8);
                MessageBox.Show("保存成功。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _hasUnsavedChanges = false;
                UpdateWindowTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EditSelectedTitle()
        {
            if (_urlList.SelectedIndices.Count == 0) return;
            var viewIndex = _urlList.SelectedIndices[0];
            if (viewIndex < 0 || viewIndex >= _filteredItems.Count) return;

            var target = _filteredItems[viewIndex];
            var newTitle = PromptForTitle(target.Title ?? string.Empty);
            if (newTitle == null) return;

            target.Title = newTitle;
            RefreshListView();
            if (viewIndex < _urlList.Items.Count)
            {
                _urlList.Items[viewIndex].Selected = true;
            }
            MarkUnsaved();
        }

        private void UrlList_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = _urlList.HitTest(e.Location);
            if (hit.Item != null)
            {
                _urlList.SelectedItems.Clear();
                hit.Item.Selected = true;
            }
        }

        private void MarkUnsaved()
        {
            _hasUnsavedChanges = true;
            UpdateWindowTitle();
        }

        private void UpdateWindowTitle()
        {
            Text = _hasUnsavedChanges ? "网址批量打开工具 *" : "网址批量打开工具";
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_hasUnsavedChanges) return;
            var result = MessageBox.Show(
                "检测到未保存改动，是否保存后退出？\n选择“是”=保存并退出，选择“否”=不保存直接退出，选择“取消”=返回继续编辑。",
                "退出确认",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == DialogResult.Yes)
            {
                SaveUrls();
                if (_hasUnsavedChanges)
                {
                    // 保存失败时仍保持未保存状态，阻止退出
                    e.Cancel = true;
                }
            }
        }

        private void ChooseFile()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择网址预设文件";
                dialog.Filter = "JSON 文件|*.json|文本文件|*.txt|所有文件|*.*";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    LoadFromFile(dialog.FileName);
                }
            }
        }

        private async Task OpenAllUrlsAsync()
        {
            var urls = _items.Select(x => x.Url).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (urls.Count == 0)
            {
                MessageBox.Show("当前没有可打开的网址。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var failed = await Task.Run(() => OpenUrlsWithSettings(urls));
            HandleOpenFailure(failed);
        }

        private async Task OpenPartialUrlsAsync()
        {
            if (_items.Count == 0)
            {
                MessageBox.Show("当前没有可选择的网址。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var form = new PartialOpenForm(_items))
            {
                if (form.ShowDialog() != DialogResult.OK) return;
                var urls = form.SelectedUrls;
                if (urls.Count == 0)
                {
                    MessageBox.Show("你还没有选择网址。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var selectedUrls = urls.Select(x => x.Item2).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                var failed = await Task.Run(() => OpenUrlsWithSettings(selectedUrls));
                HandleOpenFailure(failed);
            }
        }

        private BrowserOpenFailure OpenUrlsWithSettings(List<string> urls)
        {
            if (urls == null || urls.Count == 0) return null;
            var delayMs = (int)Math.Round(Math.Max(0.1, Math.Min(10.0, _appSettings.openIntervalSeconds)) * 1000.0);
            for (var i = 0; i < urls.Count; i++)
            {
                var url = urls[i];
                try
                {
                    if (!string.IsNullOrWhiteSpace(_appSettings.browserPath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = _appSettings.browserPath,
                            Arguments = url,
                            UseShellExecute = false
                        });
                    }
                    else
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    return new BrowserOpenFailure
                    {
                        Url = url,
                        ErrorMessage = ex.Message
                    };
                }
                Thread.Sleep(delayMs);
            }
            return null;
        }

        private void HandleOpenFailure(BrowserOpenFailure failed)
        {
            if (failed == null) return;
            var browserTip = string.IsNullOrWhiteSpace(_appSettings.browserName) ? "系统默认浏览器" : _appSettings.browserName;
            var result = MessageBox.Show(
                "打开链接失败，已停止后续打开。\n浏览器：" + browserTip + "\n链接：" + failed.Url + "\n原因：" + failed.ErrorMessage +
                "\n\n是否现在更换浏览器？\n选择“是”进入设置，选择“否”取消。",
                "打开失败",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );
            if (result == DialogResult.Yes)
            {
                OpenSettings();
            }
        }

        private AppSettings LoadAppSettings()
        {
            try
            {
                if (File.Exists(_settingsFile))
                {
                    var text = File.ReadAllText(_settingsFile, Encoding.UTF8);
                    var s = Json.Deserialize<AppSettings>(text);
                    if (s != null)
                    {
                        if (s.openIntervalSeconds < 0.1 || s.openIntervalSeconds > 10.0) s.openIntervalSeconds = 0.1;
                        return s;
                    }
                }
            }
            catch
            {
            }
            return new AppSettings
            {
                browserName = string.Empty,
                browserPath = string.Empty,
                openIntervalSeconds = 0.1
            };
        }

        private void SaveAppSettings()
        {
            var json = Json.Serialize(_appSettings);
            File.WriteAllText(_settingsFile, json, Encoding.UTF8);
        }

        private void OpenSettings()
        {
            using (var form = new SettingsForm(_appSettings))
            {
                if (form.ShowDialog() != DialogResult.OK) return;
                _appSettings = form.ResultSettings;
                SaveAppSettings();
            }
        }

        private void UpdateFileLabel()
        {
            _fileLabel.Text = "当前文件：" + _currentFile;
        }

        private static string EscapeJson(string text)
        {
            if (text == null) return string.Empty;
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string PrettyJson(LinkStore store)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"version\": 1,");
            sb.AppendLine("  \"links\": [");
            for (var i = 0; i < store.links.Count; i++)
            {
                var link = store.links[i];
                sb.AppendLine("    {");
                sb.AppendLine("      \"order\": " + link.order + ",");
                sb.AppendLine("      \"title\": \"" + EscapeJson(link.title ?? string.Empty) + "\",");
                sb.AppendLine("      \"url\": \"" + EscapeJson(link.url ?? string.Empty) + "\"");
                sb.Append("    }");
                if (i < store.links.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  ]");
            sb.Append("}");
            return sb.ToString();
        }
    }

    public class BrowserOpenFailure
    {
        public string Url;
        public string ErrorMessage;
    }

    public class PartialOpenForm : Form
    {
        private sealed class Candidate
        {
            public string Title;
            public string Url;
        }

        private readonly List<Candidate> _candidates = new List<Candidate>();
        private readonly List<Candidate> _selectedInOrder = new List<Candidate>();
        private readonly ListView _list = new ListView();

        public List<Tuple<string, string>> SelectedUrls
        {
            get { return _selectedInOrder.Select(x => Tuple.Create(x.Title, x.Url)).ToList(); }
        }

        public PartialOpenForm(IEnumerable<UrlEntry> items)
        {
            Text = "选择部分打开";
            Width = 900;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(800, 450);
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);

            // 加载应用图标
            var partialIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(partialIconPath)) Icon = new Icon(partialIconPath);

            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Url)) continue;
                _candidates.Add(new Candidate
                {
                    Title = item.Title ?? string.Empty,
                    Url = item.Url
                });
            }

            BuildUi();
            RefreshRows();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            Controls.Add(root);

            var tip = new Label
            {
                Text = "双击列表项加入打开队列；再次双击可取消。标题前数字表示打开顺序。",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(47, 77, 112)
            };
            root.Controls.Add(tip, 0, 0);

            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.MultiSelect = false;
            _list.Font = new Font("Segoe UI", 9.5F);
            _list.BackColor = Color.FromArgb(252, 254, 255);
            _list.GridLines = true;
            _list.Columns.Add("顺序", 70);
            _list.Columns.Add("标题", 260);
            _list.Columns.Add("网址", 520);
            _list.DoubleClick += (s, e) => ToggleSelected();
            root.Controls.Add(_list, 0, 1);

            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            root.Controls.Add(btnPanel, 0, 2);

            var okBtn = new ShadowButton { Text = "按选择顺序打开", Width = 148, Height = 32, DialogResult = DialogResult.OK };
            var cancelBtn = new ShadowButton { Text = "取消", Width = 80, Height = 32, DialogResult = DialogResult.Cancel };
            var clearBtn = new ShadowButton { Text = "清空选择", Width = 90, Height = 32 };
            clearBtn.Click += (s, e) =>
            {
                _selectedInOrder.Clear();
                RefreshRows();
            };

            btnPanel.Controls.Add(okBtn);
            btnPanel.Controls.Add(cancelBtn);
            btnPanel.Controls.Add(clearBtn);
            AcceptButton = okBtn;
            CancelButton = cancelBtn;
        }

        private void ToggleSelected()
        {
            if (_list.SelectedIndices.Count == 0) return;
            var idx = _list.SelectedIndices[0];
            if (idx < 0 || idx >= _candidates.Count) return;
            var candidate = _candidates[idx];
            var existingIdx = _selectedInOrder.FindIndex(x => x.Url == candidate.Url);
            if (existingIdx >= 0)
            {
                _selectedInOrder.RemoveAt(existingIdx);
            }
            else
            {
                _selectedInOrder.Add(candidate);
            }
            RefreshRows();
        }

        private void RefreshRows()
        {
            _list.Items.Clear();
            foreach (var candidate in _candidates)
            {
                var orderIdx = _selectedInOrder.FindIndex(x => x.Url == candidate.Url);
                var order = orderIdx >= 0 ? (orderIdx + 1).ToString() : string.Empty;
                var title = candidate.Title ?? string.Empty;
                var shownTitle = orderIdx >= 0 ? ("[" + (orderIdx + 1) + "] " + title) : title;

                var row = new ListViewItem(order);
                row.SubItems.Add(shownTitle);
                row.SubItems.Add(candidate.Url);
                _list.Items.Add(row);
            }
        }
    }

    public class SettingsForm : Form
    {
        private readonly AppSettings _editing;
        private readonly Label _browserLabel = new Label();
        private readonly TextBox _intervalInput = new TextBox();
        public AppSettings ResultSettings { get { return _editing; } }

        public SettingsForm(AppSettings current)
        {
            _editing = new AppSettings
            {
                browserName = current == null ? string.Empty : (current.browserName ?? string.Empty),
                browserPath = current == null ? string.Empty : (current.browserPath ?? string.Empty),
                openIntervalSeconds = current == null ? 0.1 : current.openIntervalSeconds
            };
            if (_editing.openIntervalSeconds < 0.1 || _editing.openIntervalSeconds > 10.0) _editing.openIntervalSeconds = 0.1;

            Text = "设置";
            Width = 680;
            Height = 360;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(620, 320);
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);

            // 加载应用图标
            var settingsIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(settingsIconPath)) Icon = new Icon(settingsIconPath);

            BuildUi();
            RefreshBrowserLabel();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(14)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            Controls.Add(root);

            var browserPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
            browserPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            browserPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            root.Controls.Add(browserPanel, 0, 0);

            var detectBtn = new ShadowButton { Text = "检测并选择浏览器", Width = 160, Height = 32 };
            detectBtn.Click += (s, e) => DetectAndChooseBrowser();
            browserPanel.Controls.Add(detectBtn, 0, 0);

            _browserLabel.Dock = DockStyle.Fill;
            _browserLabel.TextAlign = ContentAlignment.MiddleLeft;
            _browserLabel.ForeColor = Color.FromArgb(42, 74, 110);
            browserPanel.Controls.Add(_browserLabel, 0, 1);

            var intervalPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
            intervalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            intervalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            intervalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(intervalPanel, 0, 1);

            var intervalLabel = new Label
            {
                Text = "链接打开间隔（秒）：",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false
            };
            intervalPanel.Controls.Add(intervalLabel, 0, 0);

            _intervalInput.Dock = DockStyle.None;
            _intervalInput.Width = 120;
            _intervalInput.Height = 28;
            _intervalInput.Anchor = AnchorStyles.Left;
            _intervalInput.Text = _editing.openIntervalSeconds.ToString("0.0");
            _intervalInput.Margin = new Padding(3, 2, 3, 2);
            intervalPanel.Controls.Add(_intervalInput, 1, 0);

            var intervalHint = new Label
            {
                Text = "范围 0.1 ~ 10（例如 0.5 或 2）",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(84, 104, 130)
            };
            intervalPanel.Controls.Add(intervalHint, 2, 0);

            var infoLabel = new Label
            {
                Text = "当前版本：" + GetAppVersion() + "    创作者：CBs2N",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(47, 77, 112)
            };
            root.Controls.Add(infoLabel, 0, 3);

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            root.Controls.Add(btnPanel, 0, 4);

            var okBtn = new ShadowButton { Text = "保存设置", Width = 100, Height = 32, DialogResult = DialogResult.None };
            okBtn.Click += (s, e) => SaveAndClose();
            var cancelBtn = new ShadowButton { Text = "取消", Width = 80, Height = 32, DialogResult = DialogResult.Cancel };
            btnPanel.Controls.Add(okBtn);
            btnPanel.Controls.Add(cancelBtn);
            CancelButton = cancelBtn;
        }

        private void DetectAndChooseBrowser()
        {
            var consent = MessageBox.Show(
                "将检测本机已安装且可用的浏览器，是否继续？",
                "浏览器检测",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            if (consent != DialogResult.Yes) return;

            var list = DetectBrowsers();
            if (list.Count == 0)
            {
                MessageBox.Show("未检测到可用浏览器，将继续使用系统默认浏览器。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selected = ChooseBrowserDialog(list);
            if (selected == null) return;
            _editing.browserName = selected.Name;
            _editing.browserPath = selected.Path;
            RefreshBrowserLabel();
        }

        private void RefreshBrowserLabel()
        {
            if (string.IsNullOrWhiteSpace(_editing.browserName))
            {
                _browserLabel.Text = "当前浏览器：系统默认浏览器";
            }
            else
            {
                _browserLabel.Text = "当前浏览器：" + _editing.browserName + "（" + _editing.browserPath + "）";
            }
        }

        private void SaveAndClose()
        {
            double sec;
            if (!double.TryParse((_intervalInput.Text ?? string.Empty).Trim(), out sec))
            {
                MessageBox.Show("间隔输入无效，请输入数字（0.1 到 10）。", "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _intervalInput.Focus();
                _intervalInput.SelectAll();
                return;
            }
            if (sec < 0.1 || sec > 10.0)
            {
                MessageBox.Show("间隔超出范围，请输入 0.1 到 10 之间的数字。", "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _intervalInput.Focus();
                _intervalInput.SelectAll();
                return;
            }
            _editing.openIntervalSeconds = Math.Round(sec, 2);
            DialogResult = DialogResult.OK;
            Close();
        }

        private static List<BrowserInfo> DetectBrowsers()
        {
            var candidates = new List<BrowserInfo>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddRegistryBrowsers(candidates, visited, Registry.LocalMachine, @"SOFTWARE\Clients\StartMenuInternet");
            AddRegistryBrowsers(candidates, visited, Registry.CurrentUser, @"SOFTWARE\Clients\StartMenuInternet");
            AddKnownPath(candidates, visited, "Microsoft Edge", @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe");
            AddKnownPath(candidates, visited, "Microsoft Edge", @"C:\Program Files\Microsoft\Edge\Application\msedge.exe");
            AddKnownPath(candidates, visited, "Google Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe");
            AddKnownPath(candidates, visited, "Google Chrome", @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe");
            AddKnownPath(candidates, visited, "Mozilla Firefox", @"C:\Program Files\Mozilla Firefox\firefox.exe");
            AddKnownPath(candidates, visited, "Mozilla Firefox", @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe");
            AddKnownPath(candidates, visited, "Opera", @"C:\Users\" + Environment.UserName + @"\AppData\Local\Programs\Opera\launcher.exe");
            AddKnownPath(candidates, visited, "Brave", @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe");

            return candidates.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void AddRegistryBrowsers(List<BrowserInfo> list, HashSet<string> visited, RegistryKey root, string path)
        {
            try
            {
                using (var key = root.OpenSubKey(path))
                {
                    if (key == null) return;
                    foreach (var subName in key.GetSubKeyNames())
                    {
                        using (var sub = key.OpenSubKey(subName))
                        {
                            if (sub == null) continue;
                            var displayNameObj = sub.GetValue(null);
                            var displayName = displayNameObj == null ? subName : displayNameObj.ToString();
                            using (var cmdKey = sub.OpenSubKey(@"shell\open\command"))
                            {
                                if (cmdKey == null) continue;
                                var cmdObj = cmdKey.GetValue(null);
                                if (cmdObj == null) continue;
                                var exePath = ExtractExePath(cmdObj.ToString());
                                if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) continue;
                                if (visited.Add(exePath))
                                {
                                    list.Add(new BrowserInfo { Name = displayName, Path = exePath });
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void AddKnownPath(List<BrowserInfo> list, HashSet<string> visited, string name, string path)
        {
            if (File.Exists(path) && visited.Add(path))
            {
                list.Add(new BrowserInfo { Name = name, Path = path });
            }
        }

        private static string ExtractExePath(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return string.Empty;
            var text = command.Trim();
            if (text.StartsWith("\""))
            {
                var next = text.IndexOf("\"", 1, StringComparison.Ordinal);
                if (next > 1) return text.Substring(1, next - 1);
            }
            var exeIndex = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIndex > 0) return text.Substring(0, exeIndex + 4);
            var firstSpace = text.IndexOf(' ');
            return firstSpace > 0 ? text.Substring(0, firstSpace) : text;
        }

        private BrowserInfo ChooseBrowserDialog(List<BrowserInfo> browsers)
        {
            using (var form = new Form())
            {
                form.Text = "选择浏览器";
                form.StartPosition = FormStartPosition.CenterParent;
                form.Width = 760;
                form.Height = 420;
                form.MinimumSize = new Size(640, 360);

                // 加载应用图标
                var chooseIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(chooseIconPath)) form.Icon = new Icon(chooseIconPath);

                var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(12) };
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
                form.Controls.Add(root);

                var listBox = new ListBox { Dock = DockStyle.Fill };
                foreach (var b in browsers) listBox.Items.Add(b);
                if (listBox.Items.Count > 0) listBox.SelectedIndex = 0;
                root.Controls.Add(listBox, 0, 0);

                var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
                var okBtn = new ShadowButton { Text = "确定", Width = 80, Height = 32, DialogResult = DialogResult.OK };
                var cancelBtn = new ShadowButton { Text = "取消", Width = 80, Height = 32, DialogResult = DialogResult.Cancel };
                btnPanel.Controls.Add(okBtn);
                btnPanel.Controls.Add(cancelBtn);
                root.Controls.Add(btnPanel, 0, 1);
                form.AcceptButton = okBtn;
                form.CancelButton = cancelBtn;

                if (form.ShowDialog() != DialogResult.OK) return null;
                if (listBox.SelectedItem == null) return null;
                return (BrowserInfo)listBox.SelectedItem;
            }
        }

        private static string GetAppVersion()
        {
            return "1.4.1";
        }
    }
}
