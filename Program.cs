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
        private bool _hasUnsavedChanges;
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public MainForm()
        {
            Text = "网址批量打开工具";
            Width = 1020;
            Height = 620;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 540);
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            DoubleBuffered = true;

            _defaultFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "saved_urls.json");
            _currentFile = _defaultFile;

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

            await Task.Run(() =>
            {
                foreach (var url in urls)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                        // 某个网址失败不影响后续网址继续打开
                    }
                    Thread.Sleep(100);
                }
            });
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

                await Task.Run(() =>
                {
                    foreach (var item in urls)
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = item.Item2,
                                UseShellExecute = true
                            });
                        }
                        catch
                        {
                        }
                        Thread.Sleep(100);
                    }
                });
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
}
