# DataComparison

一個 WPF 桌面工具:連線 SQL Server、瀏覽資料表、用按鈕組出查詢條件、疊放多次查詢結果做比對,並可以把資料表「還原」回之前某次查詢時的狀態。

## 環境需求

- .NET Framework 4.0(Client Profile)
- Windows + SQL Server(透過 `System.Data.SqlClient` 連線)
- 建置:MSBuild(x86),例如:

```bash
"C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" DataComparison.sln /p:Configuration=Debug /p:Platform=x86
```

## 功能

### 登入(MainWindow)
- 輸入伺服器名稱、資料庫名稱、登入帳號、密碼,測試連線成功後才能進入主畫面。
- 連線失敗會停用「確認」按鈕並倒數 3 秒。
- 登入成功後,連線資訊(密碼經過混淆處理,不是明碼)會存到執行檔旁的 `Data\SqlServerConnection.xml`,下次開啟會自動帶入並自動登入。
- 由「登出」返回本畫面時,欄位一樣會帶入上次存的連線資訊,但**不會自動登入**,需要手動按「確認」。

### 資料比對主畫面(DataComparisonWindow)
- 以分頁(Tab)方式管理多組獨立的查詢作業,每個分頁互不影響;可新增(`+`)、關閉(`x`)分頁,分頁標題會依選取的資料表自動命名。
- 右上角「登出」按鈕:返回登入畫面,並保留已儲存的登入資訊(不刪除),同時關閉本畫面。

### 單一分頁內容(ComparisonTabContent)
- 左側:資料表清單(可篩選)。
- 右側:選定資料表後列出所有欄位,點擊欄位按鈕會插入到 WHERE 條件輸入框;另提供 `and`/`or`/`not`/`exists`/`(`/`)`/`=` 等 SQL 關鍵字按鈕,操作方式相同。
  - 數值型欄位插入 `(欄位 = )`,游標停在 `= ` 之後;文字型欄位插入 `(欄位 = '')`,游標停在單引號中間。
- 最上方的 `compid`/`subcompid` 欄位:填入後對應的欄位按鈕會自動隱藏,查詢時會自動 AND 進 WHERE 條件;數值會存到 `Data\QueryFilter.xml`,下次新增分頁時自動帶入。
- 「查詢」執行後,結果會以堆疊方式加入下方清單(最多保留 10 筆,超過會捨棄最舊的一筆),每筆結果左側有核取方塊(最多同時勾選 2 筆)。
- 「清空」會先跳出確認對話框,避免誤按。
- 「資料比對」:需勾選剛好 2 筆結果才會啟用。依主鍵比對兩份結果的每一列,不同的儲存格會標色(上面那筆標粉紅、下面那筆標淺綠);任一筆有主鍵不存在於另一邊時,整列標色。若資料表沒有主鍵則無法比對。
- 「資料復原」:需勾選剛好 1 筆結果才會啟用,開啟還原視窗。

### 資料還原(RestoreDataWindow)
- 上方顯示最新一次查詢結果,下方顯示勾選的目標結果,以主鍵比對並標色顯示差異;上下兩個表格捲動會互相同步。
- 按下「確認」會依序執行(過程中顯示半透明遮罩與 5 段式進度條):
  1. 檢查兩邊資料是否完全相同,相同則提示「資料皆相同」並中止。
  2. 逐列以主鍵檢查上方(目前狀態)的資料是否仍存在於資料庫,若有缺漏則提示「資料不完整」並中止。
  3. 組出刪除目前資料列的 DELETE 陳述式(依主鍵)。
  4. 組出還原目標資料列的 INSERT 陳述式(依下方勾選結果)。
  5. 在同一個交易(Transaction)內依序執行 DELETE、INSERT,任一步驟失敗即整個回復。
- 還原成功後,關閉本視窗,並在主畫面移除勾選項目與其後的所有查詢結果,自動重新查詢一次取得資料庫目前的最新狀態。
- 按「取消」則直接關閉,不做任何變更。

## 專案結構

```
DataComparison/
├── App.xaml(.cs)
├── MainWindow.xaml(.cs)           登入畫面
├── Fragment/
│   ├── DataComparisonWindow.xaml(.cs)   分頁殼層 + 登出
│   ├── ComparisonTabContent.xaml(.cs)   單一分頁的查詢/比對邏輯(UserControl)
│   ├── RestoreDataWindow.xaml(.cs)      還原流程
│   └── RowComparisonHelper.cs           依主鍵比對/標色的共用邏輯
└── SQLserver/
    ├── SqlServerConnectionInfo.cs       連線字串組裝
    ├── SqlServerConnectionHelper.cs     查詢/交易執行(SqlConnection 包裝)
    ├── PasswordCipher.cs                密碼混淆(Base64 + 逐字元補數)
    ├── SavedConnectionInfo.cs / SqlServerConnectionRepository.cs   登入資訊存取(Data\SqlServerConnection.xml)
    └── SavedQueryFilterInfo.cs / QueryFilterRepository.cs          compid/subcompid 存取(Data\QueryFilter.xml)
```

## 注意事項

- `Data` 資料夾(存放連線資訊、查詢條件)是執行時期在執行檔旁自動建立的,不會隨程式碼一起提供。
- 密碼儲存採自訂的簡易混淆方式(Base64 後,除了 `=` 之外每個字元做位元補數),**不是強加密**,僅供避免明碼直接寫在磁碟上,不建議用於高機敏環境。
- 本專案獨立於 VerCode/ShareAiMd 知識庫規則之外,不受其命名慣例、版控流程等規範約束。
