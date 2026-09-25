# PersistentWindows（繁體中文版）

> **這是修改版分支。** 本專案 fork 自 [kangyu-california/PersistentWindows](https://github.com/kangyu-california/PersistentWindows)，
> 基底版本 5.76。相對於上游的變更如下：
>
> - 介面、選單、對話框、通知提示與說明文件全面在地化為台灣繁體中文
> - 修正繁體中文路徑與編碼相容性問題（Win32 API 全面 Unicode 化、批次檔改以 OEM 字碼頁寫出）
> - 新增原生 Win32 快照管理對話框
> - 自動升級與說明連結改指向本分支，避免中文版被上游英文版自動覆寫
>
> 依 GNU GPL v3 授權釋出，與上游相同。原始專案的著作權歸原作者所有。

本專案要解決的是 Windows 7、10 與 11 上長年存在的[問題](https://answers.microsoft.com/en-us/windows/forum/windows_10-hardware/windows-10-multiple-display-windows-are-moved-and/2b9d5a18-45cc-4c50-b16e-fd95dbf27ff3?page=1&auth=1)：每當系統從睡眠喚醒、外接顯示器插拔、顯示器解析度變更（例如離開全螢幕遊戲），或是遠端桌面重新連線之後，視窗的位置就會被系統搬動。本專案由 [ninjacrab.com/persistent-windows](http://www.ninjacrab.com/persistent-windows/) 分支而來。

## 原始專案說明

> 什麼是 PersistentWindows？
>
> 這是一個名字取得不太好的工具程式，它會在顯示器數量或解析度變動時保存視窗的位置與大小，並在狀況恢復後還原成先前的設定。
>
> 如果您的多顯示器環境混用 DisplayPort 與其他介面，只要執行這個工具，就不必再擔心每次都得重新排列視窗。

## 主要功能

- 自動還原：持續追蹤視窗位置的變化，並自動將桌面佈局（包含工作列位置）還原成上一次相符顯示器組態下的狀態。
- 支援具有多種顯示器組態的遠端桌面工作階段。
- 新視窗會立即還原到它上一次關閉時的位置，應用程式開發者不必再自行維護視窗位置記錄。
- 擷取視窗佈局至硬碟：以 LiteDB 格式將桌面佈局手動儲存到硬碟，即使電腦重新啟動，已關閉的視窗也能還原到對應的虛擬桌面。
- 擷取快照：將桌面佈局手動儲存到記憶體，快照中會一併保留視窗的 Z 順序。每一種顯示器組態最多可儲存 36 份快照（`[0-9a-z]`）。
- 快照管理：內建原生 Win32 快照管理對話框，可檢視每一份快照實際記錄了哪些視窗，並直接刪除、重新命名或套用還原。
- 自動以 XML 格式將所有視窗（存活中與已關閉）的位置記錄保存到硬碟，因此即使程式升級、重新啟動甚至電腦重新開機，手動還原點（即快照）與自動還原點都能繼續正常運作。
- 網頁指令視窗：在各大瀏覽器中以類似 vi 編輯器的單鍵指令操作，提升瀏覽網頁的效率。
- 在前景與背景兩個位置之間高效切換視窗。
- 可暫停或恢復自動還原。
- 支援自動升級。
- 更多功能與指令請參閱[快速說明頁面](https://www.github.com/kangyu-california/PersistentWindows/blob/master/Help.md)。

## 安裝

方法一

* 在命令提示字元中執行 `winget install PersistentWindows`。實際安裝路徑為 `c:\users\[使用者]\AppData\Local\Microsoft\WinGet\Packages\kangyu-california.PersistentWindows....`，接著在一個**新開的**命令提示字元視窗中輸入 `PersistentWindows` 即可啟動程式。

方法二

- 從 [Releases](https://github.com/kangyu-california/PersistentWindows/releases) 頁面下載最新的 `PersistentWindows*.zip` 檔案。
- 將檔案解壓縮到任意目錄。
- 您可以把資料夾名稱中的版本號移除，這樣程式更新到新版本時資料夾名稱仍維持不變。

> 注意：程式可以放在任何目錄執行，但資料一律儲存於
> *C:\Users\\[使用者]\AppData\Local\PersistentWindows*

**若要讓 PersistentWindows 能夠還原具有較高權限的視窗（例如工作管理員或事件檢視器），必須以系統管理員權限執行。**

### 設定 PersistentWindows 於使用者登入時自動啟動

可以透過在**工作排程器**中建立工作，或是在**啟動資料夾**（`shell:startup`）中加入捷徑來達成。

請從以下三種方法中**擇一**使用：

**方法一：工作排程器（Windows 10／11）**

* （選用）編輯 `auto_start_pw_aux.ps1` 的第二行，自訂傳遞給 `PersistentWindows.exe` 的命令列選項。例如可以附加下列選項來停用進階功能：
    \+ " -basic_features".
* 進入安裝目錄，執行 *auto_start_pw.bat*（建議以系統管理員身分執行），即可在工作排程器中建立工作。
        <img src="https://github.com/kangyu-california/PersistentWindows/assets/59128756/e323086a-8373-4e8a-b439-3c7087550cb0" alt="auto_start_pw as administrator" width="400" />

**方法二：工作排程器（Windows 7／10／11）**

* 在安裝資料夾中建立 `pw.bat`，內容如下：
```
  start "" /B "%~dp0PersistentWindows.exe" -splash=0
```
* 以系統管理員權限開啟命令提示字元（cmd.exe），切換（cd）到 PW 的安裝資料夾，然後執行下列指令：
```
schtasks /create /sc onlogon /tn "StartPersistentWindows" /f /tr "'%~dp0pw.bat'" /rl HIGHEST
REM Override High DPI Scaling
REG ADD "HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers" /v "%~dp0PersistentWindows.exe" /t REG_SZ /d "~ HIGHDPIAWARE" /f
```

**方法三：啟動資料夾（Windows 7／10／11）**

* 在啟動資料夾中建立捷徑：
  * 按 `Win + R`，輸入 `shell:startup`
  * 建立 *PersistentWindows.exe* 的捷徑並放入啟動資料夾
* 若需要系統管理員權限：
  * 請改為建立一個 .vbs 檔（可命名為 *PersistentWindows as Administrator.vbs*），內容如下：
    ```
    Set objShell = CreateObject("Shell.Application")
    objShell.ShellExecute "C:\path\to\PersistentWindows.exe", "", "", "runas", 1
    ```
  * 請將指令碼中的路徑替換成您實際存放 *PersistentWindows.exe* 的位置

  <br>

  > 注意：雖然可以透過捷徑的內容選單設定「以系統管理員身分執行」，但從啟動資料夾開啟捷徑時這項設定並不會生效，因此才需要改用 .vbs 指令碼這種替代做法。

## 解除安裝

  步驟一：以系統管理員身分執行 `uninstall.bat`，刪除 PersistentWindows 建立的專屬 appdata 資料夾，並移除自動啟動工作。

  步驟二：若當初是以 winget 安裝，請執行 `winget uninstall PersistentWindows`；否則直接刪除存放 `PersistentWindows.exe` 的目錄即可。

## 使用說明

- 執行 `PersistentWindows.exe`（建議以系統管理員身分執行）。請注意本程式沒有主視窗，圖示預設會收納在工作列的系統匣中。
- 若希望圖示固定顯示在工作列上，請在工作列設定中開啟 PersistentWindows 這一項。

  <img src="showicon.png" alt="taskbar setting" width="400" />
- 在 PersistentWindows 圖示上按一下滑鼠右鍵即可開啟選單，擷取與還原等動作都在其中。
  ![image](https://github.com/kangyu-california/PersistentWindows/assets/59128756/6a196d75-7d86-4bd3-8873-4a4d65cb3c30)

- 若要還原工作列位置，請在圖示變成紅色時避免移動滑鼠。
- 有新版本可供升級時，選單中會出現升級通知。
- 選單中的「檢視記錄(&L) ...」可直接查看程式記錄，支援搜尋、複製與匯出，並會自動更新。記錄會寫成純文字檔存放於資料夾中，不需要系統管理員權限。
- 選單中的「關於(&A) ...」會顯示版本與貢獻者資訊。
- 開機時自動啟動請依照上方「設定 PersistentWindows 於使用者登入時自動啟動」一節設定；
  建議採用工作排程器，因為只有這個方式能以最高權限執行，還原工作管理員這類高權限視窗才會生效。

## 快照管理介面

從系統匣選單選擇「快照管理(&M) ...」即可開啟原生 Win32 快照管理對話框，用來解決「硬碟上存了一堆快照卻不知道存了什麼」的問題：

- 上方列表會掃描快照儲存目錄（`%LOCALAPPDATA%\PersistentWindows`，或可攜模式下的程式目錄），列出所有已儲存的具名快照與目前記憶體中的快照，並顯示**快照名稱**、**來源**、**顯示器配置**（例如 1920x1080 + 2560x1440）、**儲存時間**與**視窗總數**。
- 點選任一份快照後，下方列表會即時解析並列出該快照內記錄的每一個視窗，包含**行程名稱**、**視窗標題**、**視窗座標與大小**（X、Y、寬度、高度）以及**視窗狀態**（最大化／最小化／全螢幕／一般）。
- 操作按鈕：
  - `套用還原(&A)`：立即將畫面上的視窗還原至所選快照的佈局。
  - `刪除快照(&D)`：刪除已失效或過期的快照（會先跳出確認對話框）。
  - `重新命名(&R)`：直接在介面上修改硬碟快照的名稱。
  - `開啟快照資料夾(&F)`：以檔案總管開啟快照儲存目錄，方便手動清理。
  - `儲存目前佈局為新快照(&S) ...`：輸入名稱後將目前佈局存成新的硬碟快照，並自動重新整理清單。
  - `還原此視窗(&W)`：只還原下方清單中選取的那一個視窗，不影響其他視窗。
  - `從快照移除(&X)`：把選取的視窗記錄從快照中刪除，不會影響實際執行中的視窗。

顯示器配置欄位會標示由左至右的排列順序、解析度與桌面座標（座標 (0,0) 者標記為「主」），
與目前環境相符的快照會標記「← 目前」，清單上方並顯示目前顯示器的系統裝置名稱。

## 隱私權聲明

- PersistentWindows 為了完成工作，會收集下列資訊：
  * 視窗位置
  * 視窗大小
  * 視窗 Z 順序
  * 視窗標題文字
  * 視窗類別名稱
  * 視窗所屬行程的 ID 與命令列
  * 點選或移動視窗時按下的 Ctrl、Alt、Shift 按鍵
  * 選擇 PersistentWindows 選單項目時按下的 Ctrl、Alt、Shift 按鍵
  * 與工作列上的 PersistentWindows 圖示互動時的按鍵事件
  * 網頁指令視窗啟用（Alt + W）期間，瀏覽器中的按鍵事件（僅作為網頁指令捷徑）、滑鼠點擊／捲動事件，以及游標位置與形狀
- 鍵盤與滑鼠事件的記錄通常會在收到後 1 秒內清除。
- 視窗資訊的記錄會保存在記憶體或硬碟上的 LiteDB 檔案中，等待自動或手動還原時取用。
- PersistentWindows 會定期檢查 GitHub 儲存庫是否有新版本，此行為可在選項選單中停用。

## 已知問題

- 若 PersistentWindows 不是由自動啟動工作所執行，在非整數倍縮放的顯示器上（例如 125%、150% 等）可能會運作不正常。強烈建議從檔案總管進入 `PersistentWindows.exe` 的「內容」→「相容性」→「變更高 DPI 設定」對話框，將高 DPI 縮放行為覆寫為「應用程式」。以新的 DPI 設定重新啟動 PW 之後，請立即重新擷取一次視窗佈局至硬碟。
![image](https://github.com/kangyu-california/PersistentWindows/assets/59128756/d410aa87-4552-42da-b7a4-e9d7ab1947b1)
- 如果還原過程中有某個視窗失去回應，PersistentWindows 可能會卡在「忙碌」狀態（系統匣圖示變成紅色）。您可以在工作管理員中使用「分析等待鏈」找出問題視窗。失去回應的應用程式可能需要立即熱更新，或是直接結束它，PersistentWindows 才能繼續進行。

  本分支加入了還原逾時保護：還原若超過 90 秒仍未完成，會自動強制結束該次還原、把圖示恢復成閒置並重新開放自動擷取，並在記錄中寫下是哪些視窗造成卡住（在「檢視記錄(&L) ...」中搜尋「還原逾時」）。時限可用 `-restore_timeout` 調整，設為 0 則停用。
  請注意這是防止介面卡死的保護措施，並不能讓無回應的視窗被正確還原——該視窗仍需要您自行處理。

  <img src="https://user-images.githubusercontent.com/59128756/184041561-5389f540-c61a-4ee7-90ff-f9f725ba3682.png" alt="image" width="500"/>
  <img src="https://user-images.githubusercontent.com/59128756/187988981-b2564618-2724-4e1e-a718-cd0786a4251e.png" alt="wait chain" width="500"/>

## 回報問題或提出功能建議前請先了解

- PersistentWindows 提供了豐富的命令列選項可供自訂，完整清單請參閱[快速說明頁面](https://www.github.com/kangyu-california/PersistentWindows/blob/master/Help.md)。另可參考[如何自訂命令列選項](https://github.com/kangyu-california/PersistentWindows/discussions/313)。
- 除了平面佈局之外，視窗的 Z 順序也可以一併還原。此功能預設僅在手動還原快照時啟用；若要在自動還原時也修正 Z 順序，請以 `-fix_zorder=1` 選項執行 PersistentWindows。
- 為了協助診斷問題，最快的方式是從系統匣選單選擇「檢視記錄(&L) ...」，按「複製到剪貼簿(&C)」或「匯出文字檔(&E) ...」，再把內容附到問題回報中。
- 也可以直接附上記錄檔 `%LOCALAPPDATA%\PersistentWindows\PersistentWindows.log`（可攜模式則在程式資料夾）。本分支不再寫入 Windows 事件記錄，因此上游說明中「到事件檢視器搜尋事件識別碼 9990 與 9999」的做法不適用。
