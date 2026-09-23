## PersistentWindows 快速說明

### 命令列選項

  | 命令列選項 | 說明 |
  | --- | --- |
  | -basic_features | 停用較具爭議的功能，例如網頁指令視窗、新視窗位置即時還原、以 Alt 啟用視窗時交換視窗位置，以及未來新增的功能強化
  | -portable_mode | 將應用程式資料與設定檔（.db、.xml 等）存放在程式資料夾下的 "user_data" 子目錄中
  | -gui=0 | 不在系統匣顯示 PersistentWindows 圖示，效果等同於以服務方式執行 PersistentWindows
  | -splash=0       | PersistentWindows 啟動時不顯示啟動畫面
  | -legacy_icon    | 改用原始的圖示 ![pwIcon2small](https://github.com/user-attachments/assets/4827f67a-2ce1-4a83-86da-b4bfa6835026)
  | -silent         | 不顯示啟動畫面、不顯示泡泡提示、不寫入事件 Log
  | -capture_floating_window=0 | 停用擷取浮動子視窗與對話框視窗的位置
  | -ignore_process "notepad.exe;EXCEL" | 不還原 notepad.exe 與 EXCEL.EXE 這兩個行程的視窗
  | -care_process "notepad.exe;EXCEL" | 只還原 notepad.exe 與 EXCEL.EXE 這兩個行程的視窗
  | -debug_process "notepad.exe;EXCEL" | 在事件檢視器中輸出 notepad.exe 與 EXCEL.EXE 行程的視窗定位事件 Log。若要對所有行程除錯，請指定萬用字元 "*"
  | -no_inherit_process "notepad.exe;EXCEL" | 停用 notepad 與 EXCEL 行程的新視窗自動還原
  | -foreground_background_dual_position=0 | 關閉雙位置切換功能
  | -swap_window_pos_when_alt_activate=0 | 關閉 Alt + 點選背景視窗時與前景視窗互換位置的功能
  | -webpage_commander_window=0 | 取消註冊 Alt+W 快速鍵並關閉網頁指令視窗功能
  | -hotkey "Q" | 註冊 Alt + Q 作為啟用／停用網頁指令視窗的快速鍵，預設快速鍵為 "W"（Alt + W）
  | -ctrl_minimize_to_tray=0 | 關閉「Ctrl + 最小化視窗到通知區」的功能
  | -prompt_session_restore | 在恢復上一個工作階段並還原視窗佈局前先詢問使用者。這在網路較慢的遠端桌面工作階段中有助於縮短整體還原時間。
  | -delay_restart 5 | 5 秒後重新啟動 PersistentWindows。此選項僅在 PersistentWindows 正常啟動失敗時才需要使用。
  | -delay_auto_capture 1.0 | 將視窗移動事件與自動擷取之間的延遲調整為 1.0 秒，預設延遲為 3～4 秒。
  | *-delay_auto_restore 2.5* | 將顯示器開關事件與自動還原之間的延遲調整為 2.5 秒（預設延遲為 1 秒）。此選項有助於避開 Windows 內建還原機制與 PW 還原之間的衝突，若顯示器因為還原啟動過早而無法進入睡眠，此選項也可能有幫助。
  | -restore_timeout 90 | 還原總時限設為 90 秒，預設即為 90 秒。若還原卡在無回應的視窗上超過此時限，會強制結束該次還原、把系統匣圖示恢復成閒置，並重新開放自動擷取，同時在事件檢視器記錄是哪些視窗卡住。設為 0 可停用此保護
  | -redraw_desktop | 還原完成後重繪整個桌面，以防某些視窗的工作區沒有正確更新
  | -fix_zorder=1   | 自動還原時一併保留視窗的 Z 順序。Z 順序代表視窗在重疊視窗堆疊中的前後位置。
  | -fix_offscreen_window=0 | 關閉螢幕外視窗的自動校正
  | -fix_unminimized_window=0 | 關閉已取消最小化視窗的自動還原。使用此開關可避免啟用視窗時發生非預期的位移，這類位移在事件檢視器中會伴隨事件識別碼 9999：「restore minimized window ....」。
  |-auto_restore_new_display_session_from_db=0| 停用電腦啟動時或首次切換顯示器時從資料庫還原視窗
  |-auto_restore_existing_window_to_last_capture=1 | 開啟 PW 啟動時將既有視窗自動還原至上一次擷取的狀態
  |-auto_restore_new_window_to_last_capture=0 | 關閉將新視窗自動還原至上一次關閉位置的功能
  |-pos_match_threshold 80 | 將新視窗位置自動校正到上一次關閉位置的容許範圍設為 80 像素，預設為 40
  | -auto_restore_missing_windows=1 | 從硬碟還原時，直接還原遺失的視窗而不詢問使用者
  | -auto_restore_missing_windows=2 | 啟動時自動從硬碟還原遺失的視窗，每一個視窗還原前都會詢問使用者
  | -auto_restore_missing_windows=3 | 啟動時自動從硬碟還原遺失的視窗，且不詢問使用者
  | -invoke_multi_window_process_only_once=0 | 當同一個行程有多個視窗需要從資料庫還原時，多次啟動該應用程式。
  | -check_upgrade=0 | 停用 PersistentWindows 的升級檢查
  | -auto_upgrade=1 | 不需使用者操作即自動升級 PersistentWindows
  | -dump_window_position_history=0 | 停用視窗位置記錄的傾印
  | -restore_snapshot "0" | 還原快照 0 之後結束程式。快照編號範圍為 [0-9a-z]，另外還有 "~" 與 "`" 這兩個特殊編號，代表上一次的自動還原。請注意，由於此方式無法進行多輪還原，視窗的 Z 順序無法完整還原。
  | -capture_snapshot "0" | 擷取快照 0 之後結束程式
  | -restore_from_disk "name_of_capture" | 從硬碟上的（具名）擷取還原視窗之後結束程式。請注意，第一次還原過程中新啟動的視窗需要以相同命令列再還原一次。
  | -capture_to_disk "name_of_capture" | 將視窗擷取成硬碟上的（具名）擷取之後結束程式。
---

### 擷取／還原快照的捷徑

  | 快照指令 | 捷徑 |
  | --- | --- |
  | 擷取快照 0 | 在 PersistentWindows 圖示上連按兩下
  | 還原快照 0 | 點選 PersistentWindows 圖示
  | 擷取快照 X | 在 PersistentWindows 圖示上連按兩下，然後立即按下按鍵 X（X 代表數字 [0-9] 或字母 [a-z]）
  | 還原快照 X | 點選 PersistentWindows 圖示，然後立即按下按鍵 X
  | 復原上一次的快照還原 | Alt + 點選 PersistentWindows 圖示

### 硬碟視窗擷取／還原的捷徑

  * 若要將具名擷取存到硬碟，請按住 Ctrl 並點選選單中的「擷取視窗佈局至硬碟(&C)」，然後在彈出的對話框中輸入名稱。
  * 若要從硬碟還原具名擷取，請按住 Ctrl 並點選選單中的「從硬碟還原視窗佈局(&R)」，然後在對話框中輸入先前儲存的名稱。
  * 若要還原其他顯示器組態下的擷取，請按住 Shift 並點選選單中的「從硬碟還原視窗佈局(&R)」。

### 記錄檢視介面

  * 從系統匣選單選擇「檢視記錄(&L) ...」即可開啟。
  * 記錄會寫成純文字檔 `%LOCALAPPDATA%\PersistentWindows\PersistentWindows.log`（可攜模式則在程式資料夾），超過 2 MB 會自動輪替並保留兩份舊檔。上游原本只寫入 Windows 事件記錄，但註冊事件來源需要系統管理員權限，一般權限下甚至連查詢都會失敗，因此常常什麼都看不到；檔案記錄不需要任何特殊權限。
  * 檢視器以記錄檔為主要來源，讀取幾乎即時。記錄檔不存在時會回頭掃描 Windows 事件記錄（事件識別碼 9990 與 9999），讓舊版留下的記錄仍看得到。
  * 支援即時搜尋：輸入關鍵字會同時比對時間、類型與內容。例如輸入「逾時」可快速找出還原被強制結束的紀錄。
  * 在任一列連按兩下可查看完整內容，清單欄寬放不下的長訊息就不會被截斷。
  * 「複製到剪貼簿(&C)」與「匯出文字檔(&E) ...」會輸出目前篩選後的結果，對應回報問題時需要附上記錄的流程。匯出的檔案為帶 BOM 的 UTF-8，記事本可正確顯示中文。
  * 記錄是在背景讀取的。若需要回頭掃描 Windows 事件記錄，上萬筆可能耗時數秒，期間視窗仍可操作。
  * 最多顯示最近 1000 筆，更舊的記錄請用「開啟事件檢視器(&V)」查看。
  * 以 `-silent` 執行時不會寫入任何記錄，此時清單會是空的。

### 快照管理介面

  * 從系統匣選單選擇「快照管理(&M) ...」即可開啟原生 Win32 快照管理對話框。
  * 上方列表列出所有硬碟與記憶體快照，並顯示快照名稱、來源、顯示器配置、儲存時間與視窗總數。
  * 點選某一份快照後，下方列表會即時解析並列出該快照內記錄的每個視窗，包含行程名稱、視窗標題、座標與大小（X、Y、寬度、高度）以及視窗狀態。
  * 可直接套用還原、刪除快照、重新命名硬碟快照、以檔案總管開啟快照儲存目錄，或將目前佈局儲存為新快照。
  * 在上方列表的任一列連按兩下，等同於按下「套用還原(&A)」。
  * 下方的視窗清單會標示每個視窗當時位於哪一台顯示器（#1、#2 ...），依與顯示器重疊面積最大者判定，與 Windows 的 MonitorFromRect 一致。完全在畫面外的視窗會標示為「螢幕外」。
  * 單一視窗操作：選取下方清單中的某個視窗後，可按「還原此視窗(&W)」只還原該視窗而不影響其他視窗（連按兩下亦可），或按「從快照移除(&X)」把這筆記錄從快照中刪除。移除只影響快照內容，不會動到實際執行中的視窗。
  * 顯示器配置欄位會標示由左至右的排列順序、各顯示器的解析度與桌面座標，
    桌面座標為 (0,0) 的那一台會標記為「主」，與目前環境相符的快照則標記「← 目前」。
    清單上方另會顯示目前的顯示器環境與作業系統指派的裝置名稱（例如 `\.\DISPLAY2`）。

---
### 操控視窗位置的捷徑

* 雙位置切換讓同一個視窗可以在前景與背景兩種不同的位置與大小之間切換。

  * 啟用雙位置切換：
    * Ctrl + 移動或調整視窗大小。

  * 雙位置切換的功能：
    * 點選桌面視窗，可讓前景視窗回到它先前的背景位置與 Z 順序。
    * Ctrl + 點選桌面視窗，可讓前景視窗回到先前的 Z 順序，但保持目前的位置與大小。

  * 取消雙位置切換：
    * 直接移動或調整視窗大小（不要按住 Ctrl 鍵）。
  * 若要把背景的雙位置切換視窗帶到前景，但*不*還原成先前的前景位置：
    * 啟用該視窗時按住 Ctrl／Shift／Alt 之中任一個按鍵。
* 交換前景視窗與某個背景視窗的位置
  * Alt + 點選該背景視窗
* 將（隱形或位於螢幕外的）前景視窗移到主顯示器中央
  * Shift + 點選 PersistentWindows 圖示
* 將前景視窗置於所有視窗之後（類似 Windows 內建的 Alt+Esc 捷徑，但該捷徑對遠端桌面視窗內的視窗無效）：
  * Alt + 點選桌面視窗，可將前景視窗移到 Z 順序的最底層。
  * Ctrl + Alt + 點選 PersistentWindows 圖示，可將*最大化*的前景視窗移到 Z 順序的最底層。
* 將視窗隱藏到工作列的通知區：
  * Ctrl + 點選最小化按鈕
* 關閉視窗並永久清除它的位置記錄
  * Ctrl + 關閉該視窗
* 啟用或停用非最上層視窗（例如子視窗或對話框）的自動還原：
  * 若要將子視窗／對話框納入自動擷取與還原，請用滑鼠移動該視窗一次。
  * 若要將某個視窗排除在自動擷取與還原之外，請按住 Ctrl + Shift 並移動該視窗。

### 網頁指令視窗

* 網頁指令視窗會攔截指令捷徑（這些捷徑經過精心設計，單手即可操作）並轉譯給底層的瀏覽器視窗，讓瀏覽網頁的效率最大化。
* 在任何瀏覽器視窗（Chrome、Edge、Firefox、Opera、Brave、Vivaldi 等）中按下 Alt + W 即可啟用或停用網頁指令視窗。
* 啟用後，會有一個藍色的小型指令視窗跟隨滑鼠游標一起顯示。

  | 指令捷徑 | 轉譯為 | 說明 |
  | --- | --- | --- |
  | 1-8 | Ctrl + #n | 選擇第 n 個索引標籤
  | 9 | Ctrl + 9 | 選擇最右邊的索引標籤
  | TAB | Ctrl + TAB | 選擇右邊的下一個索引標籤
  | Q | Shift + Ctrl + TAB | 選擇左邊的上一個索引標籤
  | W | Ctrl + W | 關閉索引標籤
  | T | Ctrl + T | 開新索引標籤
  | R | Ctrl + R | 重新載入網頁
  | A | Ctrl + L | 編輯網址（Address）
  | S | Ctrl + F | 在目前頁面中搜尋
  | X（或 /） | / | 搜尋網頁
  | E | Home | 捲動到頁首
  | D | End | 捲動到頁尾
  | F | Alt + Right | 前往（Forward）下一個網址
  | B | Alt + Left | 返回（Backward）上一個網址
  | G | Ctrl + Shift + A | 前往（Goto）並選擇某個索引標籤（僅支援 Chrome／Edge／Brave）
  | V | | 前往上一個瀏覽過（Visited）的索引標籤
  | C | | 複製（Copy）目前的索引標籤
  | U（或 Shift + T） | Ctrl + Shift + T | 復原（Undo）已關閉的索引標籤
  | N | Ctrl + N | 開新瀏覽器視窗（New）
  | H | Left | 向左捲動
  | J | Down | 向下捲動
  | K | Up | 向上捲動
  | L | Right | 向右捲動
  | Space（或在指令視窗中按滑鼠左鍵） | PgDn | 下一頁
  | P（或在指令視窗中按滑鼠右鍵） | PgUp | 上一頁
  | Z | | 將下一個背景瀏覽器視窗帶到前景
  | ~ | | 將第二個背景瀏覽器視窗帶到前景
  | Alt + 點選指令視窗 || 將這次滑鼠點擊傳送給底層的瀏覽器視窗

### 其他功能

* 將預設的應用程式圖示替換成自訂圖示：
  * 將自訂的 .ico（或 .png）檔案重新命名為 `pwIcon.ico`（或 `pwIcon.png`），複製到 PersistentWindows 的程式資料夾，或是複製到 `C:/Users/<YOUR_ID>/AppData/Local/PersistentWindows/`。
  * 將另一個 ico/png 檔案複製到同一個目錄並重新命名為 `pwIconBusy.*`。這個圖示會在 PersistentWindows 忙於還原視窗時顯示。
  * 再將另一個 ico/png 檔案複製到同一個目錄並重新命名為 `pwIconUpdate.*`。這個圖示會在有新版 PersistentWindows 可供升級時顯示。
