using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace DataComparison.SQLserver
{
    /// <summary>
    /// 負責把 compid/subcompid 暫存成 exe 同層 Data\QueryFilter.xml,以及讀回。
    /// </summary>
    public static class QueryFilterRepository
    {
        private const string FileName = "QueryFilter.xml";

        public static string GetFilePath()
        {
            return Path.Combine(SqlServerConnectionRepository.GetDataFolderPath(), FileName);
        }

        public static void Save(string compId, string subCompId)
        {
            var folderPath = SqlServerConnectionRepository.GetDataFolderPath();
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var savedInfo = new SavedQueryFilterInfo
            {
                CompId = compId,
                SubCompId = subCompId
            };

            var serializer = new XmlSerializer(typeof(SavedQueryFilterInfo));
            using (var writer = new StreamWriter(GetFilePath(), false, Encoding.UTF8))
            {
                serializer.Serialize(writer, savedInfo);
            }
        }

        /// <summary>
        /// 讀取失敗(檔案不存在或內容損毀)一律回傳 null。
        /// </summary>
        public static SavedQueryFilterInfo Load()
        {
            if (!File.Exists(GetFilePath()))
            {
                return null;
            }

            try
            {
                var serializer = new XmlSerializer(typeof(SavedQueryFilterInfo));
                using (var reader = new StreamReader(GetFilePath(), Encoding.UTF8))
                {
                    return (SavedQueryFilterInfo)serializer.Deserialize(reader);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
