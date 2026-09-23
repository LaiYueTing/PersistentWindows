## 請替換成您需要的命令列參數
$arguments = "-splash=0"

$executablePath = $PSScriptRoot + "\PersistentWindows.exe"

## 建立註冊表設定，讓 PersistentWindows.exe 以高 DPI 感知模式執行
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers" -Name $executablePath -Value "~ HIGHDPIAWARE"

## 可依需要自行修改工作名稱
$taskName = "StartPersistentWindows" + $env:username
$taskDescription = "此工作會在 " + $env:username + " 登入時自動啟動。"

$existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue

if ($existingTask -ne $null) {
    Write-Host "移除既有的工作。"
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

$action = New-ScheduledTaskAction -Execute `"$executablePath`"
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:username

## 將 PW 行程的優先權設為低於標準
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -Priority 8
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Description $taskDescription

$task = Get-ScheduledTask -TaskName $taskName
$taskSettings = $task.Settings
$taskSettings.ExecutionTimeLimit = "PT0S" # 解除執行時間限制
Set-ScheduledTask -TaskName $taskName -Settings $taskSettings

$task.Actions[0].Arguments = $arguments
Set-ScheduledTask -TaskName $taskName -TaskPath $task.TaskPath -Action $task.Actions

## 設定此工作以最高權限執行
$principal = New-ScheduledTaskPrincipal -UserId $env:username  -RunLevel Highest
$task.Principal = $principal
Set-ScheduledTask -TaskName $taskName -TaskPath $task.TaskPath -Principal $principal