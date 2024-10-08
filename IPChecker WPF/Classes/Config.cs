using IPChecker_WPF.Classes;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using mainWindow = IPChecker_WPF.MainWindow;

namespace IPChecker_WPF
{
    internal class Config
    {
        private static readonly HttpClient client = new HttpClient();

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

        public static (string webhook, string ip, string discordMessage) GetStoredConfig()
        {
            string filePath = GetConfigFilePath();

            if (File.Exists(filePath))
            {
                try
                {
                    string[] lines = Crypto.DecryptFile(filePath);
                    string webhook = lines.Length > 0 ? lines[0].Trim() : string.Empty;
                    string ip = lines.Length > 1 ? lines[1].Trim() : string.Empty;
                    string discordMessage = lines.Length > 2 ? lines[2].Trim() : string.Empty;
                    return (webhook, ip, discordMessage);

                }
                catch
                {
                }
            }

            return (string.Empty, string.Empty, string.Empty);
        }

        public static void StoreConfig(string webhook, string ip, string discordMessage)
        {
            string filePath = GetConfigFilePath();
            string tempFilePath = filePath + ".tmp";
            File.WriteAllLines(tempFilePath, new[] { webhook, ip, discordMessage });
            Crypto.EncryptFile(tempFilePath, filePath);
            File.Delete(tempFilePath);
        }

        private static string GetConfigFilePath()
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Discord IP");
            if (!Directory.Exists(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }
            return Path.Combine(directory, "Config.txt");
        }
        public static async Task SendToDiscord(MainWindow mainWindow, string message)
        {
            (string webhook, _, string discordMessage) = GetStoredConfig();


            if (!string.IsNullOrWhiteSpace(webhook))
            {
                if (discordMessage == null) { discordMessage = "IP Address:"; }
                StringContent content = new StringContent($"{{\"content\": \"{message}\"}}", System.Text.Encoding.UTF8, "application/json");

                try
                {
                    HttpResponseMessage response = await client.PostAsync(webhook, content);
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorMessage = await response.Content.ReadAsStringAsync();
                        mainWindow.Dispatcher.Invoke(() =>
                        {
                            mainWindow.errorLabel.Content = $"Error sending message: {errorMessage}";
                        });
                    }
                }
                catch (Exception ex)
                {
                    mainWindow.Dispatcher.Invoke(() =>
                    {
                        mainWindow.errorLabel.Content = "Invalid Webhook: " + ex.Message;
                    });
                }
            }
            else
            {
                mainWindow.Dispatcher.Invoke(() =>
                {
                    mainWindow.errorLabel.Content = "Webhook URL is empty.";
                });
            }
        }

    }
}
