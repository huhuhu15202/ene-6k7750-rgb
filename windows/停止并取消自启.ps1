$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'MaxsunRGBBackground.exe'
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$name = 'MaxsunRGBBackground'
$value = '"' + $exe + '"'
$old = (Get-ItemProperty -LiteralPath $run -Name $name -ErrorAction SilentlyContinue).$name
if ($null -ne $old -and $old -ne $value) { throw '同名开机项指向其他程序，未修改。' }
$taskName = 'MaxsunRGBBackground-Logon'
$task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($task -and ($task.Actions.Count -ne 1 -or $task.Actions[0].Execute -ne $exe)) { throw '同名计划任务指向其他程序，未修改。' }
if ($task) { Unregister-ScheduledTask -TaskName $taskName -Confirm:$false }
if ($old -eq $value) { Remove-ItemProperty -LiteralPath $run -Name $name }
Get-CimInstance Win32_Process -Filter "Name = 'MaxsunRGBBackground.exe'" | Where-Object { $_.ExecutablePath -eq $exe } | ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction Stop }
Write-Output '已停止本程序并取消其当前用户登录自启。'
