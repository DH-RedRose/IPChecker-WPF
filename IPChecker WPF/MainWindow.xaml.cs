using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Application = System.Windows.Application;
using Timer = System.Timers.Timer;

namespace IPChecker_WPF
{
    public partial class MainWindow : Window
    {
        private static readonly HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private NotifyIcon trayIcon;
        private Timer timer;
        private int delay = 86400000;

        public MainWindow()
        {
            InitializeComponent();
            LoadConfig();
            InitializeTrayIcon();
        }

        private async void LoadConfig()
        {
            (string webhook, string storedIp, string discordMessage) = await Task.Run(() => Config.GetStoredConfig());
            WebhookTextBox.Text = webhook;
            DispalyTextBox.Text = discordMessage;
            storedIpLabel.Content = $"Stored IP: {storedIp ?? "Not set"}";
            currentIpLabel.Content = $"Current IP: {await Config.GetExternalIpAddress()}";

            if (!string.IsNullOrWhiteSpace(webhook))
            {
                StartCheckingIp(webhook);
            }
        }
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            (string webhook, string storedIp, string discordMessage) = await Task.Run(() => Config.GetStoredConfig());
            await Config.SendToDiscord(this, $"Application Started - {discordMessage} {storedIp}");
        }
        private void Window_Closed(object sender, EventArgs e)
        {
            _ = OnApplicationExit();
        }
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            timer?.Dispose();
            trayIcon.Dispose();
        }

        private void InitializeTrayIcon()
        {
            trayIcon = new NotifyIcon
            {
                Icon = Properties.Resources.IPCheckIcon,
                Visible = true,
                Text = "IP Monitor"
            };

            ContextMenuStrip contextMenu = new ContextMenuStrip();
            _ = contextMenu.Items.Add("Open", null, (s, e) => Show());
            _ = contextMenu.Items.Add("Exit", null, (s, e) => Application.Current.Shutdown());
            trayIcon.ContextMenuStrip = contextMenu;

            trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    Show();
                }
            };

            this.Closing += (s, e) =>
            {
                e.Cancel = true;
                Hide();
            };
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string webhookUrl = WebhookTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(webhookUrl) || !webhookUrl.StartsWith("https://discord.com/api/webhooks/"))
            {
                errorLabel.Content = "Please enter a valid webhook URL.";
                return;
            }

            string currentIp = await Config.GetExternalIpAddress();
            Config.StoreConfig(WebhookTextBox.Text, currentIp, DispalyTextBox.Text);
            storedIpLabel.Content = $"Stored IP: {currentIp}";

            await Config.SendToDiscord(this, $"Connected Webhook! {DispalyTextBox.Text}:{currentIp} will re-display in 24 hours");
            errorLabel.Content = "Webhook URL saved and IP updated!";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private async Task OnApplicationExit()
        {
            string message = "IPCheck is closing. Have a good day!";
            await Config.SendToDiscord(this, message);
        }

        private void StartCheckingIp(string webhook)
        {
            if (timer != null) return;

            timer = new Timer(delay);
            timer.Elapsed += async (sender, e) => await CheckIpAddress(webhook);
            timer.Start();

            _ = Task.Run(() => CheckIpAddress(webhook));
        }

        private async Task CheckIpAddress(string webhook)
        {
            (string _, string storedIp, string discordMessage) = Config.GetStoredConfig();
            string currentIp = await GetExternalIpAddress();

            if (storedIp == currentIp || string.IsNullOrEmpty(currentIp)) return;

            Config.StoreConfig(webhook, currentIp, discordMessage);
            await Config.SendToDiscord(this, $"{discordMessage} {currentIp}");

            Dispatcher.Invoke(() =>
            {
                currentIpLabel.Content = $"Current IP: {currentIp}";
                storedIpLabel.Content = $"Stored IP: {storedIp}";
            });
        }

        public static async Task<string> GetExternalIpAddress()
        {
            try
            {
                return await client.GetStringAsync("https://api.ipify.org");
            }
            catch
            {
                return null;
            }
        }
        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            }
        }
    }
}
