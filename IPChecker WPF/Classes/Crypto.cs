using System;
using System.IO;
using System.Management;
using System.Security.Cryptography;

namespace IPChecker_WPF.Classes
{
    public static class Crypto
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;

        public static string GetMotherboardSerialNumber()
        {
            string serialNumber = string.Empty;

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        serialNumber = obj["SerialNumber"].ToString();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving motherboard serial number: {ex.Message}");
            }

            return serialNumber;
        }

        public static byte[] DeriveKeyFromSerialNumber(string serialNumber, byte[] salt)
        {
            if (string.IsNullOrEmpty(serialNumber))
            {
                throw new ArgumentException("Serial number cannot be null or empty.", nameof(serialNumber));
            }

            using (Rfc2898DeriveBytes deriveBytes = new Rfc2898DeriveBytes(serialNumber, salt, 10000))
            {
                return deriveBytes.GetBytes(KeySize);
            }
        }

        public static void EncryptFile(string inputFilePath, string outputFilePath)
        {
            string serialNumber = GetMotherboardSerialNumber();
            byte[] salt = new byte[SaltSize];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
            }

            byte[] key = DeriveKeyFromSerialNumber(serialNumber, salt);
            byte[] iv;

            using (Aes aes = Aes.Create())
            {
                aes.GenerateIV();
                iv = aes.IV;

                using (FileStream fileStream = new FileStream(outputFilePath, FileMode.Create))
                {
                    fileStream.Write(salt, 0, salt.Length);
                    fileStream.Write(iv, 0, iv.Length);

                    using (CryptoStream cryptoStream = new CryptoStream(fileStream, aes.CreateEncryptor(key, iv), CryptoStreamMode.Write))
                    using (FileStream inputStream = new FileStream(inputFilePath, FileMode.Open))
                    {
                        inputStream.CopyTo(cryptoStream);
                    }
                }
            }
        }

        public static string[] DecryptFile(string filePath)
        {
            string serialNumber = GetMotherboardSerialNumber();

            using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] salt = new byte[SaltSize];
                if (fileStream.Read(salt, 0, salt.Length) < salt.Length)
                {
                    throw new InvalidOperationException("Could not read the full salt.");
                }

                byte[] iv = new byte[16];
                if (fileStream.Read(iv, 0, iv.Length) < iv.Length)
                {
                    throw new InvalidOperationException("Could not read the full IV.");
                }

                byte[] key = DeriveKeyFromSerialNumber(serialNumber, salt);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (CryptoStream cryptoStream = new CryptoStream(fileStream, aes.CreateDecryptor(key, iv), CryptoStreamMode.Read))
                    using (StreamReader reader = new StreamReader(cryptoStream))
                    {
                        string decryptedContent = reader.ReadToEnd();
                        return decryptedContent.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    }
                }
            }
        }
    }
}
