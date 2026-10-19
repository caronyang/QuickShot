# QuickShot

<p>
  <img src="src/QuickShot/Assets/logo-256.png" width="96" alt="QuickShot logo" align="right" />
</p>

**常駐在系統匣的 Windows 截圖工具：按下全域快捷鍵、拖曳框選一個範圍，快照就會立刻漂浮在桌面上 ——
可以任意拖移、可選擇置頂，並有三顆極小的按鈕可以置頂、存檔、關閉。**

[English](README.md) · **繁體中文**

![version](https://img.shields.io/badge/version-1.0.0-2ea44f?style=flat-square)
![platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6?style=flat-square&logo=windows&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![license](https://img.shields.io/github/license/caronyang/QuickShot?style=flat-square)
![release](https://img.shields.io/github/v/release/caronyang/QuickShot?style=flat-square&sort=semver)
![downloads](https://img.shields.io/github/downloads/caronyang/QuickShot/total?style=flat-square)
![build](https://img.shields.io/github/actions/workflow/status/caronyang/QuickShot/build.yml?branch=main&style=flat-square)
![last commit](https://img.shields.io/github/last-commit/caronyang/QuickShot?style=flat-square)
![issues](https://img.shields.io/github/issues/caronyang/QuickShot?style=flat-square)
![i18n](https://img.shields.io/badge/i18n-English%20%7C%20%E7%B9%81%E9%AB%94%E4%B8%AD%E6%96%87-9C27B0?style=flat-square)

---

## 畫面截圖

### 實際使用情境

![五個框選快照漂浮在它們取材的頁面上](screenshot/application.png)

從一份文件頁面上框選了五個區域，每個快照都還漂浮在它原本被框取的位置。這就是 QuickShot 的用途：
把你需要的片段蒐集起來、保持在視線內，用完再存檔或關閉。

### 框選範圍

![框選時的輔助線、雙色十字游標、放大鏡與即時尺寸](screenshot/selection.png)

按下快捷鍵的瞬間桌面就被凍結並變暗，框選的區域維持原本亮度。全螢幕輔助線與雙色十字游標指出精確的像素，
小標籤顯示指標的實體像素座標，放大鏡則在拖曳時顯示 `978, 667 — 472 × 399`。按 `Esc` 或滑鼠右鍵可取消。

### 漂浮快照

![三個漂浮快照與它們的極小工具列](screenshot/snapshots.png)

每個快照都精確浮在它被框取的位置上，滑鼠移入時會浮現 18 × 18 px 的極小按鈕：
📌 置頂、💾 存檔、⌃ 收折、✕ 關閉。拖曳本體可移動，雙擊可在 1:1 與縮放顯示之間切換。

### 收折成縮圖

![同一組快照全部收折成小縮圖](screenshot/collapsed.png)

常駐程式必須在意畫面空間，所以每個快照都能收折成最大 180 × 135 px 的縮圖 —— 依然漂浮、位置不變，
需要時再精確恢復成原本尺寸。托盤選單可以一次處理全部。

### 快照數量上限

![取代對話框，顯示現有快照的縮圖](screenshot/replace-dialog.png)

預設同時最多 20 個漂浮快照。達到上限時會詢問要用這次的截圖取代哪一個（預設選取最舊的），
或者按取消放棄這次截圖。

### 設定

![繁體中文的設定對話框](screenshot/settings.png)

介面語言、快捷鍵、開機自動啟動、存檔資料夾、快照數量上限、透明度與通知。
切換語言會立即重新翻譯整個介面，不需要重新啟動。

> 這些截圖背後的桌面是程式產生的示意場景（`tools/screenshot-backdrop.py`），
> 因此畫面中除了應用程式本身以外沒有其他真實內容；第一張是實際使用截圖。

---

## 安裝

| | |
|---|---|
| **安裝版（建議）** | 從 [Releases](../../releases) 下載 `QuickShot-Setup-1.0.0.exe` 執行 —— 單一使用者安裝到 `%LocalAppData%\Programs\QuickShot`，**不需要管理員權限**，含開始功能表捷徑、反安裝程式，且「開機自動啟動」預設已勾選 |
| **免安裝可攜版** | 下載 `QuickShot.exe`（自帶執行階段，約 63 MB），放到任何位置直接執行，不需安裝 .NET |
| **精簡版** | `.\build\publish.ps1 -Framework` 產生 0.35 MB 的版本，需要 .NET 8 Desktop Runtime，記憶體用量約為一半 |

系統需求：Windows 10 1809+ 或 Windows 11，x64，不需其他元件。

---

## 使用方式

| 動作 | 操作 |
|---|---|
| 框選截圖 | `Win + Shift + Q`（可修改）、雙擊系統匣圖示，或再執行一次 `QuickShot.exe` |
| 取消框選 | `Esc` 或滑鼠右鍵 |
| 移動漂浮快照 | 拖曳快照本體 |
| 1:1 / 縮放顯示切換 | 雙擊快照本體 |
| 收折成縮圖 | `⌃` 按鈕，或右鍵選單 |
| 恢復原尺寸 | `⌄` 按鈕，或右鍵選單 |
| **全部收折／全部恢復** | 托盤選單 →「收折所有快照成縮圖」／「恢復所有快照」 |
| 存檔 | `💾` 按鈕 —— 左鍵存到預設資料夾，右鍵為「另存新檔…」 |
| 置頂 | `📌` 按鈕，或右鍵選單 |
| 關閉／全部關閉 | `✕` 按鈕；托盤選單 →「關閉所有漂浮快照」 |
| 設定 | 托盤選單 →「設定…」 |

快照會存到 `%USERPROFILE%\Pictures\QuickShot\QuickShot_<yyyyMMdd_HHmmss>.png`；
檔名重複時會自動加上 `_2`、`_3`…。

### 命令列參數

| 參數 | 效果 |
|---|---|
| （無） | 常駐啟動；若已在執行中，則請執行中的實例開始一次截圖 |
| `--capture` | 立即顯示框選遮罩 |
| `--settings` | 開啟設定對話框（同樣會轉交給執行中的實例） |
| `--collapse` | 將所有快照收折成縮圖；再執行一次即全部恢復 |

由於第二次啟動會把請求轉給執行中的實例，這些參數可以當作可腳本化的觸發器 ——
把 `QuickShot.exe --collapse` 綁到鍵盤快速鍵或 AutoHotkey 腳本，就有「一鍵把所有快照收乾淨」的功能。

---

## 技術規格

![C%23](https://img.shields.io/badge/C%23-12-239120?style=flat-square&logo=csharp&logoColor=white)
![WPF](https://img.shields.io/badge/UI-WPF-0078D6?style=flat-square)
![WinForms](https://img.shields.io/badge/tray-WinForms%20NotifyIcon-68217A?style=flat-square)
![Win32](https://img.shields.io/badge/native-RegisterHotKey%20%C2%B7%20GDI%20BitBlt-4B5563?style=flat-square)
![DPI](https://img.shields.io/badge/DPI-PerMonitorV2-blueviolet?style=flat-square)
![single exe](https://img.shields.io/badge/publish-single%20file%20self--contained-informational?style=flat-square)
![installer](https://img.shields.io/badge/installer-Inno%20Setup%206-B34F2B?style=flat-square)
![dependencies](https://img.shields.io/badge/nuget-none-success?style=flat-square)
![made with](https://img.shields.io/badge/made%20with-%E2%99%A5%20and%20P/Invoke-ff69b4?style=flat-square)

* **C# 12 / .NET 8 (LTS)**，介面使用 WPF，WinForms 僅用於系統匣 `NotifyIcon`
* **零 NuGet 相依** —— 快捷鍵、截圖、DPI 全部是 `user32`／`shcore` 的 P/Invoke
* `RegisterHotKey` 加上 message-only 的 `HwndSource` 接收全域快捷鍵（`WM_HOTKEY`）
* 以 GDI `BitBlt`（`CopyFromScreen`）截圖，`PngBitmapEncoder` 產生無損快照
* **PerMonitorV2** DPI 感知（`app.manifest`），混合 DPI 的多螢幕環境下框選座標仍精確
* Inno Setup 6 安裝程式、單一使用者免提權；發佈為自帶執行階段的單一 exe
* 英文（預設）與繁體中文，透過 XAML 資源字典與 `{res:Loc Key}` 標記擴充在執行期即時切換

---

## 設定項目

存放於 `%AppData%\QuickShot\settings.json`，同目錄會有執行日誌 `quickshot.log`。

| 鍵 | 預設值 | 說明 |
|---|---|---|
| `AutoStart` | `true` | 寫入目前使用者的 `Run` 登錄值（`HKCU\...\CurrentVersion\Run`） |
| `Language` | `"en"` | `"en"` 或 `"zh-TW"` |
| `Hotkey` | `"Win+Shift+Q"` | 任何 `Ctrl/Shift/Alt/Win + 按鍵` 組合 |
| `SaveFolder` | `…\Pictures\QuickShot` | 快照存檔位置 |
| `MaxSnapshots` | `20` | 漂浮快照數量上限 |
| `SnapshotOpacity` | `0.96` | 0.30 – 1.00 |
| `PinNewSnapshots` | `true` | 新快照預設置頂 |
| `Notifications` | `true` | 系統匣氣泡通知 |

---

## 從原始碼建置

```powershell
# 開發建置並執行
dotnet build src\QuickShot\QuickShot.csproj -c Debug
src\QuickShot\bin\Debug\net8.0-windows\QuickShot.exe

# 自帶執行階段的單一 exe  ->  build\publish\QuickShot.exe            (62.9 MB)
.\build\publish.ps1 -SkipInstaller

# 單一 exe + 安裝程式      ->  build\installer\QuickShot-Setup-1.0.0.exe  (58 MB)
.\build\publish.ps1

# 精簡版（需 .NET 8 Desktop Runtime） -> build\publish-framework\        (0.35 MB)
.\build\publish.ps1 -Framework -SkipInstaller
```

需要 .NET 8 SDK，安裝程式步驟另需 Inno Setup 6。

### 專案結構

```
src/QuickShot
├─ App.xaml(.cs)              單一實例（mutex + 具名事件）、截圖流程、設定套用
├─ Interop/                   RegisterHotKey / GDI / DPI / SetWindowPos 的 P/Invoke
├─ Models/                    AppSettings、Snapshot、HotkeyGesture（解析與顯示）
├─ Services/                  HotkeyService、TrayService、ScreenCaptureService、
│                             SnapshotManager、SettingsService、LocalizationService、Log
├─ Views/                     OverlayWindow + SelectionSession、SnapshotWindow、
│                             ReplaceSnapshotDialog、SettingsWindow
├─ Resources/                 Strings_en.xaml、Strings_zh-TW.xaml、Loc 標記擴充
└─ Themes/Styles.xaml         筆刷與極小按鈕樣式
installer/QuickShot.iss       Inno Setup 腳本
build/publish.ps1             發佈與打包
tools/                        Logo 產生器、截圖背景、端對端測試腳本
```

### 三個關鍵設計決策

1. **先截圖、再顯示遮罩。** 快捷鍵觸發的瞬間就抓取整個虛擬桌面，變暗的框選遮罩再畫出這張凍結的畫面。
   這樣就消掉了「隱藏遮罩 → 等待 → 截圖 → 還原」的競態，因此遮罩絕不可能出現在快照裡、
   放大鏡才能運作，而且完全沒有閃爍。
2. **每個螢幕一個遮罩視窗。** 跨越多個不同 DPI 縮放比例螢幕的單一視窗，會被迫用同一個 DPI 空間排版，
   導致框選座標偏移；改成每個螢幕一個視窗，再以 `GetWindowRect` 量測實際位置換算實體像素，才能精確。
3. **快照以 PNG 位元組保存，而非位圖。** 20 張 4K 全螢幕位圖約需 660 MB；改為保留無損 PNG
   加上最大 1600 px 的預覽圖，20 張快照總量大約 150 MB。存檔時直接寫出原始 PNG 位元組
   （不重新編碼、無畫質損失）；1:1 檢視時才即時解碼全解析度，離開時立刻釋放。

---

## 實測驗證

自動化端對端測試以模擬鍵盤／滑鼠驅動真實視窗，並逐像素檢查結果（`tools/e2e/`）：

| 項目 | 結果 |
|---|---|
| 快捷鍵 `Ctrl+Shift+P` → 遮罩 → 拖曳 → 漂浮 | `Snapshot #1 floated (560×400, 1/20)` |
| 出廠預設快捷鍵 `Win+Shift+Q` | `Snapshot #1 floated (560×300, 1/20)` |
| 像素精確度 | 框選的 560×400 與 560×300 與拖曳範圍完全一致 |
| 快照精確落在被框取的位置 | 以截圖差異比對確認 |
| 極小工具列（置頂／存檔／收折／關閉） | 以螢幕截圖放大 8 倍確認 |
| 存檔按鈕 → 檔案落地 | `QuickShot_20261019_115155.png`（52,990 bytes） |
| 上限測試：`MaxSnapshots=2` 連截 3 張 | `#1 (1/2)`、`#2 (2/2)`、對話框、`#3 (2/2)` |
| 全部收折 → 全部恢復（以 `EnumWindows` 量測視窗矩形） | `580×320, 420×420` → `200×116, 155×155` → 回到 `580×320, 420×420`，位置不變 |
| 單一視窗的收折按鈕 | 只有被點擊的視窗縮小，另一個維持 `420×420` |
| 極端長寬比收折 | `700×50` → `200×92`、`80×400` → `128×155`，內容置中留邊不變形 |
| 框選輔助 | 十字游標、輔助線與座標讀數與實際游標像素一致 |
| 設定視窗（en 與 zh-TW） | 所有文字皆正確在地化，所有繫結皆解析成功 |
| 連續 8 次截圖的記憶體 | 閒置 80 MB → 139 MB，之後在 143–159 MB 之間盤旋（穩定，無洩漏） |

| 版本 | 檔案大小 | 閒置工作集 | 多張快照後 |
|---|---|---|---|
| `-Framework`（需 .NET 8 Desktop Runtime） | 0.35 MB | 80 MB | 約 150 MB，穩定 |
| 預設（自帶執行階段單一 exe） | 62.9 MB | 206 MB | 約 250 MB，穩定 |

---

## 已知限制

* **獨佔全螢幕遊戲**：任何程式都無法漂浮在其上；無邊框視窗模式則可正常運作。
* **DRM 保護內容**（Netflix、Disney+ 等）截圖會是黑畫面，屬作業系統限制。
* **快捷鍵是獨佔的**：QuickShot 執行期間，該組合鍵會被本程式取走。`Win+Shift+Q` 已實測在 Windows 10
  可用；若組合已被其他程式註冊則無法註冊，程式會回報衝突而不會默默失效。
  `Win+Shift+↓` **無法使用** —— 已被 Windows 保留（實測 `RegisterHotKey` 回傳
  `ERROR_HOTKEY_ALREADY_REGISTERED`），`Win+↓`、`Win+Shift+S`、`Win+L` 同樣不可用。
* 快照視窗刻意不出現在 Alt+Tab 清單中（`WS_EX_TOOLWINDOW`）。
* 一次拖曳僅能框選一個範圍：無法跨越多個不同 DPI 縮放比例的螢幕。

---

## 開發參考資料

建置、打包與驗證所用的工具全部隨專案提供：

| 路徑 | 用途 |
|---|---|
| `tools/make-icon.py` | Logo 產生器：`--sheet` 產生 A–D 候選對照表（用來挑選定稿圖示），`--design B` 產生 `logo-*.png` 與含 7 種尺寸的 `quickshot.ico`（每個尺寸原生繪製、光學縮放） |
| `docs/design/icon-candidates.png` | 最初的圖示候選對照表，供設計參考 |
| `tools/screenshot-backdrop.py` | 產生 README 截圖所用的合成桌面背景 |
| `tools/e2e/verify.ps1` | **驗證套件**：建置、驅動真實應用程式，並逐項驗證「實測驗證」表格的每一列（截圖、像素精確度、存檔、上限、收折／恢復、極端長寬比、記憶體）。失敗時回傳非零值。 |
| `tools/e2e/screenshots.ps1` | 驅動應用程式重新產生所有 README 截圖 |
| `tools/e2e/hotkey-probe.ps1` | 詢問 Windows 哪些快捷鍵組合真正可用（`RegisterHotKey` 可用性表） |
| `%AppData%\QuickShot\quickshot.log` | 執行期日誌（INFO/DEBUG） |

---

## 授權

[MIT](LICENSE) © Caron Yang

QuickShot 與其他截圖工具並無關聯。「Windows」為 Microsoft Corporation 的商標。
