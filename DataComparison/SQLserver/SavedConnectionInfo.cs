namespace DataComparison.SQLserver
{
    /// <summary>
    /// 對應 Data\SqlServerConnection.xml 的序列化結構,密碼欄位存的是 PasswordCipher 編碼後的字串。
    /// </summary>
    public class SavedConnectionInfo
    {
        public string ServerName { get; set; }
        public string DatabaseName { get; set; }
        public string UserId { get; set; }
        public string EncodedPassword { get; set; }
    }
}
