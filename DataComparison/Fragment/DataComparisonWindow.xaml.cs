using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using DataComparison.SQLserver;

namespace DataComparison.Fragment
{
    /// <summary>
    /// DataComparisonWindow.xaml 的互動邏輯:管理多個 ComparisonTabContent 分頁。
    /// </summary>
    public partial class DataComparisonWindow : Window
    {
        private static readonly Brush SelectedTabBrush = Brushes.White;
        private static readonly Brush UnselectedTabBrush = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
        private static readonly Brush HoverBrush = new SolidColorBrush(Color.FromRgb(0xBE, 0xE6, 0xFD));
        private static readonly Brush HoverBorderBrush = new SolidColorBrush(Color.FromRgb(0x3C, 0x7F, 0xB1));

        private readonly SqlServerConnectionInfo _connectionInfo;
        private readonly List<TabEntry> _tabs = new List<TabEntry>();
        private readonly Button _addTabButton;
        private TabEntry _selectedTab;

        public DataComparisonWindow(SqlServerConnectionInfo connectionInfo)
        {
            InitializeComponent();
            _connectionInfo = connectionInfo;

            _addTabButton = new Button
            {
                Content = "+",
                Width = 26,
                Height = 22,
                Margin = new Thickness(2, 0, 2, 4),
                FontWeight = FontWeights.Bold,
                Background = UnselectedTabBrush,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Template = CreateRoundedButtonTemplate(4)
            };
            _addTabButton.Click += AddTabButton_Click;
            TabStripPanel.Children.Add(_addTabButton);

            LogoutButton.Background = UnselectedTabBrush;
            LogoutButton.BorderBrush = Brushes.Gray;
            LogoutButton.BorderThickness = new Thickness(1);
            LogoutButton.Template = CreateRoundedButtonTemplate(4);

            AddNewTab();
        }

        private void AddTabButton_Click(object sender, RoutedEventArgs e)
        {
            AddNewTab();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = new DataComparison.MainWindow(autoLogin: false);
            mainWindow.Show();
            Close();
        }

        private void AddNewTab()
        {
            var entry = new TabEntry();
            entry.Content = new ComparisonTabContent(_connectionInfo);
            entry.Content.SelectedTableChanged += (s, e) => UpdateTabTitle(entry);

            entry.TitleTextBlock = new TextBlock
            {
                Text = "new",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 0)
            };

            var titleButton = new Button
            {
                Content = entry.TitleTextBlock,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 2, 4, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleButton.Click += (s, e) => SelectTab(entry);

            var closeButton = new Button
            {
                Content = "x",
                Width = 18,
                Height = 18,
                Padding = new Thickness(0),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Template = CreateRoundedButtonTemplate(9)
            };
            closeButton.Click += (s, e) => CloseTab(entry);

            var headerContent = new StackPanel { Orientation = Orientation.Horizontal };
            headerContent.Children.Add(titleButton);
            headerContent.Children.Add(closeButton);

            entry.HeaderBorder = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1, 1, 1, 0),
                Margin = new Thickness(2, 0, 0, 0),
                Padding = new Thickness(2),
                Background = UnselectedTabBrush,
                Child = headerContent
            };

            var insertIndex = TabStripPanel.Children.IndexOf(_addTabButton);
            TabStripPanel.Children.Insert(insertIndex, entry.HeaderBorder);

            _tabs.Add(entry);
            SelectTab(entry);
        }

        private void SelectTab(TabEntry entry)
        {
            _selectedTab = entry;

            TabContentHost.Children.Clear();
            TabContentHost.Children.Add(entry.Content);

            foreach (var tab in _tabs)
            {
                tab.HeaderBorder.Background = tab == entry ? SelectedTabBrush : UnselectedTabBrush;
            }
        }

        private void CloseTab(TabEntry entry)
        {
            var index = _tabs.IndexOf(entry);
            _tabs.Remove(entry);
            TabStripPanel.Children.Remove(entry.HeaderBorder);

            if (_selectedTab != entry)
            {
                return;
            }

            if (_tabs.Count > 0)
            {
                var newIndex = Math.Min(index, _tabs.Count - 1);
                SelectTab(_tabs[newIndex]);
            }
            else
            {
                _selectedTab = null;
                TabContentHost.Children.Clear();
            }
        }

        private void UpdateTabTitle(TabEntry entry)
        {
            var tableName = entry.Content.SelectedTableName;
            entry.TitleTextBlock.Text = string.IsNullOrEmpty(tableName) ? "new" : tableName;
        }

        /// <summary>
        /// 產生一個圓角矩形外觀的 Button 樣板,外觀完全由 Background/BorderBrush/BorderThickness 決定,
        /// 呼叫端把寬高設成一樣、半徑設成寬高一半即可做出正圓形按鈕。
        /// </summary>
        private static ControlTemplate CreateRoundedButtonTemplate(double cornerRadius)
        {
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "ButtonBorder";
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
            borderFactory.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            borderFactory.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            borderFactory.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });

            var contentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(contentPresenterFactory);

            var template = new ControlTemplate(typeof(Button)) { VisualTree = borderFactory };

            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, HoverBrush, "ButtonBorder"));
            hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, HoverBorderBrush, "ButtonBorder"));
            template.Triggers.Add(hoverTrigger);

            return template;
        }

        private class TabEntry
        {
            public ComparisonTabContent Content;
            public Border HeaderBorder;
            public TextBlock TitleTextBlock;
        }
    }
}
