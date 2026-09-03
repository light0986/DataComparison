using System;
using System.Windows;
using System.Windows.Threading;
using DataComparison.Fragment;
using DataComparison.SQLserver;

namespace DataComparison
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int CooldownSeconds = 3;
        private const string ConfirmButtonDefaultText = "確認";

        private DispatcherTimer _cooldownTimer;
        private int _cooldownSecondsRemaining;
        private readonly bool _autoLogin;

        public MainWindow() : this(true)
        {
        }

        /// <summary>
        /// autoLogin=false 用於登出流程:欄位一樣會帶入上次存的連線資訊,但不會自動嘗試連線,
        /// 需要使用者自己按「確認」才會真的登入。
        /// </summary>
        public MainWindow(bool autoLogin)
        {
            InitializeComponent();
            _autoLogin = autoLogin;
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var connectionInfo = SqlServerConnectionRepository.Load();
            if (connectionInfo == null)
            {
                return;
            }

            ServerNameTextBox.Text = connectionInfo.ServerName;
            DatabaseNameTextBox.Text = connectionInfo.DatabaseName;
            UserIdTextBox.Text = connectionInfo.UserId;
            LoginPasswordBox.Password = connectionInfo.Password;

            if (_autoLogin)
            {
                AttemptLogin(connectionInfo);
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var connectionInfo = BuildConnectionInfoFromFields();
            AttemptLogin(connectionInfo);
        }

        private SqlServerConnectionInfo BuildConnectionInfoFromFields()
        {
            return new SqlServerConnectionInfo
            {
                ServerName = ServerNameTextBox.Text.Trim(),
                DatabaseName = DatabaseNameTextBox.Text.Trim(),
                IntegratedSecurity = false,
                UserId = UserIdTextBox.Text.Trim(),
                Password = LoginPasswordBox.Password
            };
        }

        private void AttemptLogin(SqlServerConnectionInfo connectionInfo)
        {
            string errorMessage;
            bool success = SqlServerConnectionHelper.TestConnection(connectionInfo, out errorMessage);

            if (success)
            {
                MessageBox.Show("連線成功", "連線設定", MessageBoxButton.OK, MessageBoxImage.Information);
                SqlServerConnectionRepository.Save(connectionInfo);

                var dataComparisonWindow = new DataComparisonWindow(connectionInfo);
                dataComparisonWindow.Show();
                Close();
            }
            else
            {
                MessageBox.Show("連線失敗:" + errorMessage, "連線設定", MessageBoxButton.OK, MessageBoxImage.Error);
                StartCooldown();
            }
        }

        private void StartCooldown()
        {
            _cooldownSecondsRemaining = CooldownSeconds;
            ConfirmButton.IsEnabled = false;
            ConfirmButton.Content = _cooldownSecondsRemaining.ToString();

            _cooldownTimer = new DispatcherTimer();
            _cooldownTimer.Interval = TimeSpan.FromSeconds(1);
            _cooldownTimer.Tick += CooldownTimer_Tick;
            _cooldownTimer.Start();
        }

        private void CooldownTimer_Tick(object sender, EventArgs e)
        {
            _cooldownSecondsRemaining--;

            if (_cooldownSecondsRemaining <= 0)
            {
                _cooldownTimer.Stop();
                _cooldownTimer.Tick -= CooldownTimer_Tick;
                _cooldownTimer = null;

                ConfirmButton.Content = ConfirmButtonDefaultText;
                ConfirmButton.IsEnabled = true;
            }
            else
            {
                ConfirmButton.Content = _cooldownSecondsRemaining.ToString();
            }
        }
    }
}
