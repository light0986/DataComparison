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
    /// RestoreDataWindow.xaml 的互動邏輯。上方顯示最後一筆查詢結果,下方顯示使用者勾選的那一筆,
    /// 兩者依主鍵值比對(同一主鍵存在於兩邊逐欄比對,只存在單邊的整列標色):
    /// 上方標粉紅色、下方標淡綠色。按下確認後,會驗證資料並把資料庫從上方的狀態還原成下方的狀態
    /// (先 DELETE 再 INSERT)。
    /// </summary>
    public partial class RestoreDataWindow : Window
    {
        private static readonly Brush DifferenceBrushTop = Brushes.Pink;
        private static readonly Brush DifferenceBrushBottom = Brushes.LightGreen;

        private readonly DataTable _topTable;
        private readonly DataTable _bottomTable;
        private readonly SqlServerConnectionInfo _connectionInfo;
        private readonly string _tableName;
        private readonly List<string> _primaryKeyColumns;

        private ScrollViewer _topScrollViewer;
        private ScrollViewer _bottomScrollViewer;
        private bool _isSyncingScroll;

        public bool RestoreCompleted { get; private set; }

        public RestoreDataWindow(
            DataTable lastTable,
            DataTable checkedTable,
            SqlServerConnectionInfo connectionInfo,
            string tableName,
            List<string> primaryKeyColumns)
        {
            InitializeComponent();

            _topTable = lastTable;
            _bottomTable = checkedTable;
            _connectionInfo = connectionInfo;
            _tableName = tableName;
            _primaryKeyColumns = primaryKeyColumns;

            TopDataGrid.EnableRowVirtualization = false;
            TopDataGrid.EnableColumnVirtualization = false;
            BottomDataGrid.EnableRowVirtualization = false;
            BottomDataGrid.EnableColumnVirtualization = false;

            TopDataGrid.AutoGeneratingColumn += (s, e) => e.Column.Header = new TextBlock { Text = e.PropertyName };
            BottomDataGrid.AutoGeneratingColumn += (s, e) => e.Column.Header = new TextBlock { Text = e.PropertyName };

            TopDataGrid.ItemsSource = _topTable.DefaultView;
            BottomDataGrid.ItemsSource = _bottomTable.DefaultView;

            Loaded += RestoreDataWindow_Loaded;
        }

        private void RestoreDataWindow_Loaded(object sender, RoutedEventArgs e)
        {
            HighlightDifferences();

            _topScrollViewer = RowComparisonHelper.FindVisualChild<ScrollViewer>(TopDataGrid);
            _bottomScrollViewer = RowComparisonHelper.FindVisualChild<ScrollViewer>(BottomDataGrid);

            if (_topScrollViewer != null && _bottomScrollViewer != null)
            {
                _topScrollViewer.ScrollChanged += TopScrollViewer_ScrollChanged;
                _bottomScrollViewer.ScrollChanged += BottomScrollViewer_ScrollChanged;
            }
        }

        private void TopScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            SyncScroll(_topScrollViewer, _bottomScrollViewer);
        }

        private void BottomScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            SyncScroll(_bottomScrollViewer, _topScrollViewer);
        }

        private void SyncScroll(ScrollViewer source, ScrollViewer target)
        {
            if (_isSyncingScroll)
            {
                return;
            }

            _isSyncingScroll = true;
            target.ScrollToHorizontalOffset(source.HorizontalOffset);
            target.ScrollToVerticalOffset(source.VerticalOffset);
            _isSyncingScroll = false;
        }

        private void HighlightDifferences()
        {
            if (_primaryKeyColumns == null || _primaryKeyColumns.Count == 0)
            {
                return;
            }

            RowComparisonHelper.HighlightByPrimaryKey(
                _topTable, TopDataGrid,
                _bottomTable, BottomDataGrid,
                _primaryKeyColumns,
                DifferenceBrushTop, DifferenceBrushBottom);
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var confirmResult = MessageBox.Show("是否確定要還原資料?", "資料還原", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmResult != MessageBoxResult.Yes)
            {
                return;
            }

            if (_primaryKeyColumns == null || _primaryKeyColumns.Count == 0)
            {
                MessageBox.Show("這個資料表沒有主鍵,無法安全還原。", "資料還原", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ShowMask();

            try
            {
                if (RowComparisonHelper.AreIdenticalByPrimaryKey(_topTable, _bottomTable, _primaryKeyColumns))
                {
                    HideMask();
                    MessageBox.Show("資料皆相同", "資料還原", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                AdvanceProgress();

                using (var helper = new SqlServerConnectionHelper(_connectionInfo))
                {
                    foreach (DataRow row in _topTable.Rows)
                    {
                        if (!RowExistsInDatabase(helper, row))
                        {
                            HideMask();
                            MessageBox.Show("資料不完整", "資料還原", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                    AdvanceProgress();

                    var deleteCommands = BuildDeleteCommands();
                    AdvanceProgress();

                    var insertCommands = BuildInsertCommands();
                    AdvanceProgress();

                    var allCommands = new List<SqlCommandText>();
                    allCommands.AddRange(deleteCommands);
                    allCommands.AddRange(insertCommands);
                    helper.ExecuteInTransaction(allCommands);
                    AdvanceProgress();
                }

                RestoreCompleted = true;
                HideMask();
                Close();
            }
            catch (Exception ex)
            {
                HideMask();
                MessageBox.Show("還原失敗:" + ex.Message, "資料還原", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool RowExistsInDatabase(SqlServerConnectionHelper helper, DataRow row)
        {
            var whereClauses = new List<string>();
            var parameters = new List<SqlParameter>();

            for (int i = 0; i < _primaryKeyColumns.Count; i++)
            {
                var columnName = _primaryKeyColumns[i];
                var paramName = "@pk" + i;
                whereClauses.Add(columnName + " = " + paramName);
                parameters.Add(CreateParameter(paramName, row[columnName], _topTable.Columns[columnName].DataType));
            }

            var sql = string.Format("SELECT COUNT(*) FROM {0} WHERE {1}", _tableName, string.Join(" AND ", whereClauses));
            var count = (int)helper.ExecuteScalar(sql, parameters.ToArray());
            return count > 0;
        }

        private List<SqlCommandText> BuildDeleteCommands()
        {
            var commands = new List<SqlCommandText>();

            foreach (DataRow row in _topTable.Rows)
            {
                var whereClauses = new List<string>();
                var parameters = new List<SqlParameter>();

                for (int i = 0; i < _primaryKeyColumns.Count; i++)
                {
                    var columnName = _primaryKeyColumns[i];
                    var paramName = "@pk" + i;
                    whereClauses.Add(columnName + " = " + paramName);
                    parameters.Add(CreateParameter(paramName, row[columnName], _topTable.Columns[columnName].DataType));
                }

                var sql = string.Format("DELETE FROM {0} WHERE {1}", _tableName, string.Join(" AND ", whereClauses));
                commands.Add(new SqlCommandText { CommandText = sql, Parameters = parameters.ToArray() });
            }

            return commands;
        }

        private List<SqlCommandText> BuildInsertCommands()
        {
            var commands = new List<SqlCommandText>();
            var columns = _bottomTable.Columns.Cast<DataColumn>().ToList();

            foreach (DataRow row in _bottomTable.Rows)
            {
                var paramNames = new List<string>();
                var parameters = new List<SqlParameter>();

                for (int i = 0; i < columns.Count; i++)
                {
                    var paramName = "@c" + i;
                    paramNames.Add(paramName);
                    parameters.Add(CreateParameter(paramName, row[columns[i].ColumnName], columns[i].DataType));
                }

                var sql = string.Format(
                    "INSERT INTO {0} ({1}) VALUES ({2})",
                    _tableName,
                    string.Join(", ", columns.Select(c => c.ColumnName)),
                    string.Join(", ", paramNames));
                commands.Add(new SqlCommandText { CommandText = sql, Parameters = parameters.ToArray() });
            }

            return commands;
        }

        /// <summary>
        /// 直接用 SqlParameter(name, value) 建參數時,值若剛好是 DBNull,ADO.NET 會把型態預設猜成
        /// nvarchar,遇到目標欄位是 image/varbinary 等型態就會衝突。這裡改用 DataColumn 實際的 CLR
        /// 型態明確指定 SqlDbType,避免這個問題。
        /// </summary>
        private static SqlParameter CreateParameter(string paramName, object value, Type columnClrType)
        {
            var parameter = new SqlParameter(paramName, value);

            if (value == null || value == DBNull.Value)
            {
                parameter.Value = DBNull.Value;
                parameter.SqlDbType = InferSqlDbType(columnClrType);
            }

            return parameter;
        }

        private static SqlDbType InferSqlDbType(Type clrType)
        {
            if (clrType == typeof(byte[]))
            {
                return SqlDbType.Image;
            }
            if (clrType == typeof(int))
            {
                return SqlDbType.Int;
            }
            if (clrType == typeof(long))
            {
                return SqlDbType.BigInt;
            }
            if (clrType == typeof(short))
            {
                return SqlDbType.SmallInt;
            }
            if (clrType == typeof(byte))
            {
                return SqlDbType.TinyInt;
            }
            if (clrType == typeof(decimal))
            {
                return SqlDbType.Decimal;
            }
            if (clrType == typeof(double) || clrType == typeof(float))
            {
                return SqlDbType.Float;
            }
            if (clrType == typeof(DateTime))
            {
                return SqlDbType.DateTime;
            }
            if (clrType == typeof(bool))
            {
                return SqlDbType.Bit;
            }
            if (clrType == typeof(Guid))
            {
                return SqlDbType.UniqueIdentifier;
            }

            return SqlDbType.NVarChar;
        }

        private void ShowMask()
        {
            RestoreProgressBar.Value = 0;
            MaskOverlay.Visibility = Visibility.Visible;
            RefreshUi();
        }

        private void HideMask()
        {
            MaskOverlay.Visibility = Visibility.Collapsed;
        }

        private void AdvanceProgress()
        {
            RestoreProgressBar.Value += 1;
            RefreshUi();
        }

        private void RefreshUi()
        {
            Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
