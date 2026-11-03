$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'MaxsunRGBBackground.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw '未找到后台程序。' }
$taskName = 'MaxsunRGBBackground-Logon'
$task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($task -and ($task.Actions.Count -ne 1 -or $task.Actions[0].Execute -ne $exe)) { throw '同名计划任务指向其他程序，未覆盖。' }
if (-not $task) {
    $user = "$env:USERDOMAIN\$env:USERNAME"
    $action = New-ScheduledTaskAction -Execute $exe -WorkingDirectory $PSScriptRoot
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $user
    $trigger.Delay = 'PT30S'
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew
    $principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
    Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description '登录后启动铭瑄 RGB 慢速循环变色灯' | Out-Null
} else { Enable-ScheduledTask -TaskName $taskName | Out-Null }
$current = Get-CimInstance Win32_Process -Filter "Name = 'MaxsunRGBBackground.exe'" | Where-Object { $_.ExecutablePath -eq $exe }
if (-not $current) { Start-ScheduledTask -TaskName $taskName }
Write-Output "已设置当前用户登录后延迟 30 秒自启：$exe"
