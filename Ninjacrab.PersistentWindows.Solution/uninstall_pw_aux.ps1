$executablePath = $PSScriptRoot + "\PersistentWindows.exe"

## 移除讓 PersistentWindows.exe 以高 DPI 感知模式執行的登錄設定
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers" -Name $executablePath

## 可依需要自行修改工作名稱
$taskName = "StartPersistentWindows" + $env:username
$existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue

if ($existingTask -ne $null) {
    Write-Host "移除既有的工作。"
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

$app_path = $env:LOCALAPPDATA + "\PersistentWindows"
Remove-Item -Path $app_path -Recurse -Force