namespace DataComparison.SQLserver
{
    /// <summary>
    /// 對應 Data\QueryFilter.xml 的序列化結構,暫存使用者上次輸入的 compid/subcompid。
    /// </summary>
    public class SavedQueryFilterInfo
    {
        public string CompId { get; set; }
        public string SubCompId { get; set; }
    }
}
