using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace DataComparison.SQLserver
{
    /// <summary>
    /// 負責把連線設定存成 exe 同層 Data\SqlServerConnection.xml,以及讀回。
    /// </summary>
    public static class SqlServerConnectionRepository
    {
        private const string DataFolderName = "Data";
        private const string FileName = "SqlServerConnection.xml";

        public static string GetDataFolderPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DataFolderName);
        }

        public static string GetFilePath()
        {
            return Path.Combine(GetDataFolderPath(), FileName);
        }

        public static bool Exists()
        {
            return File.Exists(GetFilePath());
        }

        public static void Save(SqlServerConnectionInfo connectionInfo)
        {
            var folderPath = GetDataFolderPath();
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var savedInfo = new SavedConnectionInfo
            {
                ServerName = connectionInfo.ServerName,
                DatabaseName = connectionInfo.DatabaseName,
                UserId = connectionInfo.UserId,
                EncodedPassword = PasswordCipher.Encode(connectionInfo.Password)
            };

            var serializer = new XmlSerializer(typeof(SavedConnectionInfo));
            using (var writer = new StreamWriter(GetFilePath(), false, Encoding.UTF8))
            {
                serializer.Serialize(writer, savedInfo);
            }
        }

        /// <summary>
        /// 讀取失敗(檔案不存在或內容損毀)一律回傳 null,由呼叫端退回手動登入畫面。
        /// </summary>
        public static SqlServerConnectionInfo Load()
        {
            if (!Exists())
            {
                return null;
            }

            try
            {
                var serializer = new XmlSerializer(typeof(SavedConnectionInfo));
                using (var reader = new StreamReader(GetFilePath(), Encoding.UTF8))
                {
                    var savedInfo = (SavedConnectionInfo)serializer.Deserialize(reader);

                    return new SqlServerConnectionInfo
                    {
                        ServerName = savedInfo.ServerName,
                        DatabaseName = savedInfo.DatabaseName,
                        IntegratedSecurity = false,
                        UserId = savedInfo.UserId,
                        Password = PasswordCipher.Decode(savedInfo.EncodedPassword)
                    };
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
