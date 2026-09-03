using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DataComparison.Fragment
{
    /// <summary>
    /// 依主鍵值比對兩個 DataTable 並在對應的 DataGrid 上色。同一主鍵在兩邊都存在時逐欄比對,
    /// 只存在單邊的整列都標色(不再依 RowIndex 對應)。
    /// </summary>
    public static class RowComparisonHelper
    {
        public static void HighlightByPrimaryKey(
            DataTable topTable, DataGrid topGrid,
            DataTable bottomTable, DataGrid bottomGrid,
            List<string> primaryKeyColumns,
            Brush topBrush, Brush bottomBrush)
        {
            topGrid.UpdateLayout();
            bottomGrid.UpdateLayout();

            ResetCellBackgrounds(topGrid, topTable);
            ResetCellBackgrounds(bottomGrid, bottomTable);

            var bottomIndexByKey = BuildKeyIndex(bottomTable, primaryKeyColumns);
            var matchedBottomRows = new HashSet<int>();
            var columnCount = Math.Min(topTable.Columns.Count, bottomTable.Columns.Count);

            for (int topRow = 0; topRow < topTable.Rows.Count; topRow++)
            {
                var key = BuildKey(topTable.Rows[topRow], primaryKeyColumns);
                int bottomRow;

                if (bottomIndexByKey.TryGetValue(key, out bottomRow))
                {
                    matchedBottomRows.Add(bottomRow);

                    for (int column = 0; column < columnCount; column++)
                    {
                        var topValue = topTable.Rows[topRow][column];
                        var bottomValue = bottomTable.Rows[bottomRow][column];

                        if (!ValuesEqual(topValue, bottomValue))
                        {
                            SetCellBackground(topGrid, topRow, column, topBrush);
                            SetCellBackground(bottomGrid, bottomRow, column, bottomBrush);
                        }
                    }
                }
                else
                {
                    HighlightWholeRow(topGrid, topRow, topTable.Columns.Count, topBrush);
                }
            }

            for (int bottomRow = 0; bottomRow < bottomTable.Rows.Count; bottomRow++)
            {
                if (!matchedBottomRows.Contains(bottomRow))
                {
                    HighlightWholeRow(bottomGrid, bottomRow, bottomTable.Columns.Count, bottomBrush);
                }
            }
        }

        /// <summary>
        /// 依主鍵比對兩個 DataTable 的內容是否完全相同(列數、每個主鍵對應的每一欄都相同)。
        /// </summary>
        public static bool AreIdenticalByPrimaryKey(DataTable topTable, DataTable bottomTable, List<string> primaryKeyColumns)
        {
            if (topTable.Rows.Count != bottomTable.Rows.Count || topTable.Columns.Count != bottomTable.Columns.Count)
            {
                return false;
            }

            var bottomIndexByKey = BuildKeyIndex(bottomTable, primaryKeyColumns);
            var columnCount = topTable.Columns.Count;

            foreach (DataRow topRow in topTable.Rows)
            {
                var key = BuildKey(topRow, primaryKeyColumns);
                int bottomRowIndex;
                if (!bottomIndexByKey.TryGetValue(key, out bottomRowIndex))
                {
                    return false;
                }

                var bottomRow = bottomTable.Rows[bottomRowIndex];
                for (int column = 0; column < columnCount; column++)
                {
                    if (!ValuesEqual(topRow[column], bottomRow[column]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void HighlightWholeRow(DataGrid grid, int rowIndex, int columnCount, Brush brush)
        {
            for (int column = 0; column < columnCount; column++)
            {
                SetCellBackground(grid, rowIndex, column, brush);
            }
        }

        private static Dictionary<string, int> BuildKeyIndex(DataTable table, List<string> primaryKeyColumns)
        {
            var indexByKey = new Dictionary<string, int>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                var key = BuildKey(table.Rows[i], primaryKeyColumns);
                if (!indexByKey.ContainsKey(key))
                {
                    indexByKey[key] = i;
                }
            }

            return indexByKey;
        }

        private static string BuildKey(DataRow row, List<string> primaryKeyColumns)
        {
            var parts = new string[primaryKeyColumns.Count];
            for (int i = 0; i < primaryKeyColumns.Count; i++)
            {
                var value = row[primaryKeyColumns[i]];
                parts[i] = (value == null || value == DBNull.Value) ? string.Empty : value.ToString();
            }

            return string.Join("", parts);
        }

        public static bool ValuesEqual(object a, object b)
        {
            var textA = (a == null || a == DBNull.Value) ? string.Empty : a.ToString();
            var textB = (b == null || b == DBNull.Value) ? string.Empty : b.ToString();
            return string.Equals(textA, textB, StringComparison.Ordinal);
        }

        private static void ResetCellBackgrounds(DataGrid grid, DataTable table)
        {
            for (int row = 0; row < table.Rows.Count; row++)
            {
                for (int column = 0; column < table.Columns.Count; column++)
                {
                    var cell = GetCell(grid, row, column);
                    if (cell != null)
                    {
                        cell.ClearValue(DataGridCell.BackgroundProperty);
                    }
                }
            }
        }

        private static void SetCellBackground(DataGrid grid, int rowIndex, int columnIndex, Brush brush)
        {
            var cell = GetCell(grid, rowIndex, columnIndex);
            if (cell != null)
            {
                cell.Background = brush;
            }
        }

        private static DataGridCell GetCell(DataGrid grid, int rowIndex, int columnIndex)
        {
            var row = grid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DataGridRow;
            if (row == null)
            {
                return null;
            }

            var presenter = FindVisualChild<DataGridCellsPresenter>(row);
            if (presenter == null)
            {
                return null;
            }

            return presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
        }

        public static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            var childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                var typed = child as T;
                if (typed != null)
                {
                    return typed;
                }

                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }
    }
}
