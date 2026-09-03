using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DataComparison.SQLserver;

namespace DataComparison.Fragment
{
    /// <summary>
    /// ComparisonTabContent.xaml 的互動邏輯。每個分頁對應一個獨立的實例,狀態彼此不互相影響。
    /// </summary>
    public partial class ComparisonTabContent : UserControl
    {
        private const int QueryCooldownSeconds = 3;
        private const string QueryButtonDefaultText = "查詢";
        private const int MaxResultEntries = 10;
        private const int MaxCheckedEntries = 2;

        private static readonly HashSet<string> NumericDataTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "int", "bigint", "smallint", "tinyint", "decimal", "numeric", "float", "real", "money", "smallmoney", "bit"
        };

        private static readonly Brush DifferenceBrushUpper = Brushes.Pink;
        private static readonly Brush DifferenceBrushLower = Brushes.LightGreen;

        private readonly List<ResultEntry> _resultEntries = new List<ResultEntry>();

        private readonly SqlServerConnectionInfo _connectionInfo;
        private List<string> _allTableNames = new List<string>();
        private List<string> _currentTableColumns = new List<string>();
        private Dictionary<string, string> _currentTableColumnTypes = new Dictionary<string, string>();
        private List<string> _currentTablePrimaryKeyColumns = new List<string>();
        private string _selectedTableName;

        private DispatcherTimer _queryCooldownTimer;
        private int _queryCooldownSecondsRemaining;

        public event EventHandler SelectedTableChanged;

        public string SelectedTableName
        {
            get { return _selectedTableName; }
        }

        public ComparisonTabContent(SqlServerConnectionInfo connectionInfo)
        {
            InitializeComponent();
            _connectionInfo = connectionInfo;

            var savedFilter = QueryFilterRepository.Load();
            if (savedFilter != null)
            {
                CompIdTextBox.Text = savedFilter.CompId;
                SubCompIdTextBox.Text = savedFilter.SubCompId;
            }

            LoadTableNames();
            UpdateQueryButtonSelectionState();
        }

        private void CompIdTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshColumnButtons(ColumnFilterTextBox.Text.Trim());
        }

        private void SubCompIdTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshColumnButtons(ColumnFilterTextBox.Text.Trim());
        }

        private void LoadTableNames()
        {
            try
            {
                using (var helper = new SqlServerConnectionHelper(_connectionInfo))
                {
                    var table = helper.ExecuteQuery(
                        "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME");

                    _allTableNames = table.Rows.Cast<DataRow>()
                        .Select(row => row["TABLE_NAME"].ToString())
                        .ToList();
                }

                RefreshTableListBox(string.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show("讀取資料表清單失敗:" + ex.Message, "資料比對", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshTableListBox(string filterText)
        {
            TableListBox.Items.Clear();

            IEnumerable<string> filtered = string.IsNullOrEmpty(filterText)
                ? _allTableNames
                : _allTableNames.Where(name => name.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0);

            foreach (var name in filtered)
            {
                TableListBox.Items.Add(name);
            }
        }

        private void TableFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshTableListBox(TableFilterTextBox.Text.Trim());
        }

        private void TableListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedTableName = TableListBox.SelectedItem as string;
            ColumnFilterTextBox.Text = string.Empty;
            UpdateQueryButtonSelectionState();

            if (string.IsNullOrEmpty(_selectedTableName))
            {
                _currentTableColumns = new List<string>();
                _currentTableColumnTypes = new Dictionary<string, string>();
                _currentTablePrimaryKeyColumns = new List<string>();
                ColumnButtonsPanel.Children.Clear();
                RaiseSelectedTableChanged();
                return;
            }

            LoadColumnsForSelectedTable();
            RaiseSelectedTableChanged();
        }

        private void RaiseSelectedTableChanged()
        {
            if (SelectedTableChanged != null)
            {
                SelectedTableChanged(this, EventArgs.Empty);
            }
        }

        private void LoadColumnsForSelectedTable()
        {
            try
            {
                using (var helper = new SqlServerConnectionHelper(_connectionInfo))
                {
                    var parameter = new SqlParameter("@tableName", _selectedTableName);
                    var table = helper.ExecuteQuery(
                        "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=@tableName ORDER BY ORDINAL_POSITION",
                        parameter);

                    _currentTableColumns = table.Rows.Cast<DataRow>()
                        .Select(row => row["COLUMN_NAME"].ToString())
                        .ToList();

                    _currentTableColumnTypes = table.Rows.Cast<DataRow>()
                        .ToDictionary(row => row["COLUMN_NAME"].ToString(), row => row["DATA_TYPE"].ToString());

                    var pkParameter = new SqlParameter("@tableName", _selectedTableName);
                    var pkTable = helper.ExecuteQuery(
                        "SELECT kcu.COLUMN_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc " +
                        "JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu " +
                        "ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME AND tc.TABLE_NAME = kcu.TABLE_NAME " +
                        "WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY' AND tc.TABLE_NAME = @tableName " +
                        "ORDER BY kcu.ORDINAL_POSITION",
                        pkParameter);

                    _currentTablePrimaryKeyColumns = pkTable.Rows.Cast<DataRow>()
                        .Select(row => row["COLUMN_NAME"].ToString())
                        .ToList();
                }

                RefreshColumnButtons(string.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show("讀取欄位清單失敗:" + ex.Message, "資料比對", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshColumnButtons(string filterText)
        {
            ColumnButtonsPanel.Children.Clear();

            IEnumerable<string> filtered = string.IsNullOrEmpty(filterText)
                ? _currentTableColumns
                : _currentTableColumns.Where(name => name.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0);

            var hideCompId = !string.IsNullOrEmpty(CompIdTextBox.Text);
            var hideSubCompId = !string.IsNullOrEmpty(SubCompIdTextBox.Text);

            foreach (var columnName in filtered)
            {
                if (hideCompId && string.Equals(columnName, "compid", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (hideSubCompId && string.Equals(columnName, "subcompid", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var button = new Button
                {
                    Content = new TextBlock { Text = columnName },
                    Tag = columnName,
                    Margin = new Thickness(3),
                    Padding = new Thickness(6, 2, 6, 2)
                };
                button.Click += ColumnButton_Click;
                ColumnButtonsPanel.Children.Add(button);
            }
        }

        private void ColumnFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshColumnButtons(ColumnFilterTextBox.Text.Trim());
        }

        private void WhereInsertButton_Click(object sender, RoutedEventArgs e)
        {
            var text = (string)((Button)sender).Tag;
            InsertIntoWhereTextBox(text, text.Length);
        }

        private void ColumnButton_Click(object sender, RoutedEventArgs e)
        {
            var columnName = (string)((Button)sender).Tag;

            string dataType;
            _currentTableColumnTypes.TryGetValue(columnName, out dataType);

            if (dataType != null && NumericDataTypes.Contains(dataType))
            {
                var text = "(" + columnName + " = )";
                InsertIntoWhereTextBox(text, text.Length - 1);
            }
            else
            {
                var text = "(" + columnName + " = '')";
                InsertIntoWhereTextBox(text, text.Length - 2);
            }
        }

        private void InsertIntoWhereTextBox(string text, int caretOffsetWithinText)
        {
            var caretIndex = WhereConditionTextBox.IsFocused
                ? WhereConditionTextBox.CaretIndex
                : WhereConditionTextBox.Text.Length;

            var prefix = caretIndex > 0 ? " " : string.Empty;
            var insertText = prefix + text;

            WhereConditionTextBox.Text = WhereConditionTextBox.Text.Insert(caretIndex, insertText);
            WhereConditionTextBox.CaretIndex = caretIndex + prefix.Length + caretOffsetWithinText;
            WhereConditionTextBox.Focus();
        }

        private void QueryButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteQuery();
        }

        private void ExecuteQuery()
        {
            var compId = CompIdTextBox.Text.Trim();
            var subCompId = SubCompIdTextBox.Text.Trim();
            var whereText = WhereConditionTextBox.Text.Trim();

            var conditions = new List<string>();
            if (!string.IsNullOrEmpty(compId))
            {
                conditions.Add(string.Format("(compid = '{0}')", compId));
            }
            if (!string.IsNullOrEmpty(subCompId))
            {
                conditions.Add(string.Format("(subcompid = '{0}')", subCompId));
            }
            if (!string.IsNullOrEmpty(whereText))
            {
                conditions.Add(string.Format("({0})", whereText));
            }

            var combinedWhere = string.Join(" and ", conditions);
            var sql = string.IsNullOrEmpty(combinedWhere)
                ? string.Format("SELECT * FROM {0}", _selectedTableName)
                : string.Format("SELECT * FROM {0} WHERE {1}", _selectedTableName, combinedWhere);

            QueryFilterRepository.Save(compId, subCompId);

            try
            {
                using (var helper = new SqlServerConnectionHelper(_connectionInfo))
                {
                    var table = helper.ExecuteQuery(sql);
                    AddResultDataGrid(table);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("查詢失敗:" + ex.Message, "資料比對", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            UpdateUiEnabledState();
            StartQueryCooldown();
        }

        private void AddResultDataGrid(DataTable table)
        {
            var entry = new ResultEntry { Table = table };

            entry.Grid = new DataGrid
            {
                ItemsSource = table.DefaultView,
                AutoGenerateColumns = true,
                IsReadOnly = true,
                Height = 180,
                EnableRowVirtualization = false,
                EnableColumnVirtualization = false
            };
            entry.Grid.AutoGeneratingColumn += (s, e) => e.Column.Header = new TextBlock { Text = e.PropertyName };

            entry.CheckBox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 8, 8, 0)
            };
            entry.CheckBox.Checked += (s, e) => UpdateCheckboxAndButtonStates();
            entry.CheckBox.Unchecked += (s, e) => UpdateCheckboxAndButtonStates();

            var rowPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            DockPanel.SetDock(entry.CheckBox, Dock.Left);
            rowPanel.Children.Add(entry.CheckBox);
            rowPanel.Children.Add(entry.Grid);
            entry.Container = rowPanel;

            _resultEntries.Add(entry);
            ResultDataGrid.Children.Add(entry.Container);

            if (_resultEntries.Count > MaxResultEntries)
            {
                var oldest = _resultEntries[0];
                _resultEntries.RemoveAt(0);
                ResultDataGrid.Children.Remove(oldest.Container);
            }

            UpdateCheckboxAndButtonStates();

            ResultDataGrid.UpdateLayout();
            ResultScrollViewer.ScrollToBottom();
        }

        private void UpdateCheckboxAndButtonStates()
        {
            var checkedCount = _resultEntries.Count(r => r.CheckBox.IsChecked == true);

            foreach (var entry in _resultEntries)
            {
                entry.CheckBox.IsEnabled = entry.CheckBox.IsChecked == true || checkedCount < MaxCheckedEntries;
            }

            CompareButton.IsEnabled = checkedCount == 2;
            RestoreButton.IsEnabled = checkedCount == 1;
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            var lastEntry = _resultEntries[_resultEntries.Count - 1];
            var checkedEntry = _resultEntries.First(r => r.CheckBox.IsChecked == true);

            var restoreWindow = new RestoreDataWindow(
                lastEntry.Table,
                checkedEntry.Table,
                _connectionInfo,
                _selectedTableName,
                _currentTablePrimaryKeyColumns);
            restoreWindow.Owner = Window.GetWindow(this);
            restoreWindow.ShowDialog();

            if (restoreWindow.RestoreCompleted)
            {
                var checkedIndex = _resultEntries.IndexOf(checkedEntry);
                for (int i = _resultEntries.Count - 1; i >= checkedIndex; i--)
                {
                    var entry = _resultEntries[i];
                    ResultDataGrid.Children.Remove(entry.Container);
                    _resultEntries.RemoveAt(i);
                }

                UpdateCheckboxAndButtonStates();
                UpdateUiEnabledState();

                ExecuteQuery();
            }
        }

        private void CompareButton_Click(object sender, RoutedEventArgs e)
        {
            var checkedEntries = _resultEntries.Where(r => r.CheckBox.IsChecked == true).ToList();
            if (checkedEntries.Count != 2)
            {
                return;
            }

            if (_currentTablePrimaryKeyColumns == null || _currentTablePrimaryKeyColumns.Count == 0)
            {
                MessageBox.Show("這個資料表沒有主鍵,無法比對。", "資料比對", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var firstIndex = _resultEntries.IndexOf(checkedEntries[0]);
            var secondIndex = _resultEntries.IndexOf(checkedEntries[1]);
            var upper = firstIndex < secondIndex ? checkedEntries[0] : checkedEntries[1];
            var lower = firstIndex < secondIndex ? checkedEntries[1] : checkedEntries[0];

            RowComparisonHelper.HighlightByPrimaryKey(
                upper.Table, upper.Grid,
                lower.Table, lower.Grid,
                _currentTablePrimaryKeyColumns,
                DifferenceBrushUpper, DifferenceBrushLower);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("是否確定清空?", "資料比對", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            _resultEntries.Clear();
            ResultDataGrid.Children.Clear();
            UpdateCheckboxAndButtonStates();
            UpdateUiEnabledState();
        }

        private void ClearWhereButton_Click(object sender, RoutedEventArgs e)
        {
            WhereConditionTextBox.Text = string.Empty;
        }

        private void UpdateUiEnabledState()
        {
            var enabled = ResultDataGrid.Children.Count == 0;

            CompIdTextBox.IsEnabled = enabled;
            SubCompIdTextBox.IsEnabled = enabled;
            TableFilterTextBox.IsEnabled = enabled;
            TableListBox.IsEnabled = enabled;
            ColumnFilterTextBox.IsEnabled = enabled;
            ColumnButtonsPanel.IsEnabled = enabled;
            WhereConditionTextBox.IsEnabled = enabled;
            SqlKeywordButtonsPanel.IsEnabled = enabled;
            ClearWhereButton.IsEnabled = enabled;
        }

        private void StartQueryCooldown()
        {
            _queryCooldownSecondsRemaining = QueryCooldownSeconds;
            QueryButton.IsEnabled = false;
            QueryButton.Content = _queryCooldownSecondsRemaining.ToString();

            _queryCooldownTimer = new DispatcherTimer();
            _queryCooldownTimer.Interval = TimeSpan.FromSeconds(1);
            _queryCooldownTimer.Tick += QueryCooldownTimer_Tick;
            _queryCooldownTimer.Start();
        }

        private void QueryCooldownTimer_Tick(object sender, EventArgs e)
        {
            _queryCooldownSecondsRemaining--;

            if (_queryCooldownSecondsRemaining <= 0)
            {
                _queryCooldownTimer.Stop();
                _queryCooldownTimer.Tick -= QueryCooldownTimer_Tick;
                _queryCooldownTimer = null;

                QueryButton.Content = QueryButtonDefaultText;
                UpdateQueryButtonSelectionState();
            }
            else
            {
                QueryButton.Content = _queryCooldownSecondsRemaining.ToString();
            }
        }

        private void UpdateQueryButtonSelectionState()
        {
            QueryButton.IsEnabled = !string.IsNullOrEmpty(_selectedTableName) && _queryCooldownTimer == null;
        }

        private class ResultEntry
        {
            public DataTable Table;
            public DataGrid Grid;
            public CheckBox CheckBox;
            public FrameworkElement Container;
        }
    }
}
