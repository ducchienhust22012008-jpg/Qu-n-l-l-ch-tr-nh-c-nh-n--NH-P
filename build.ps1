param([string]$OutputName = 'Nhip.exe')
$ErrorActionPreference = 'Stop'
$verificationFolders = @('verification', 'verification/deadlines', 'verification/task-deadline', 'verification/focus-tasks', 'verification/goals-ring', 'verification/daily-statistics', 'verification/deadline-to-calendar')
foreach ($folder in $verificationFolders) { New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot $folder) -Force | Out-Null }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Cần .NET Framework 4.x của Windows để biên dịch.' }
& $compiler /nologo /target:winexe /optimize+ /utf8output /out:"$PSScriptRoot\$OutputName" "/win32icon:$PSScriptRoot\assets\nhip.ico" "/resource:$PSScriptRoot\assets\nhip.ico,Nhip.App.ico" "/resource:$PSScriptRoot\assets\nhip-brand.png,Nhip.Brand.png" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll "$PSScriptRoot\src\Model.cs" "$PSScriptRoot\src\Deadlines.cs" "$PSScriptRoot\src\App.cs" "$PSScriptRoot\src\PlanningDialogs.cs" "$PSScriptRoot\src\WeekCalendar.cs" "$PSScriptRoot\src\Tracking.cs" "$PSScriptRoot\src\DailyReports.cs" "$PSScriptRoot\src\TrackingPages.cs" "$PSScriptRoot\src\StatisticsOverview.cs" "$PSScriptRoot\src\TodayTimeline.cs" "$PSScriptRoot\src\MonthlyGoalsCard.cs" "$PSScriptRoot\src\DeadlinePage.cs" "$PSScriptRoot\src\UiRefresh.cs" "$PSScriptRoot\src\Branding.cs" "$PSScriptRoot\src\DailyStatisticsPage.cs" "$PSScriptRoot\src\DeadlineSchedulePanel.cs"
if ($LASTEXITCODE -ne 0) { throw 'Biên dịch app thất bại.' }
& $compiler /nologo /target:exe /utf8output /out:"$PSScriptRoot\verification\Tests.exe" /r:System.Web.Extensions.dll "$PSScriptRoot\src\Model.cs" "$PSScriptRoot\src\Deadlines.cs" "$PSScriptRoot\src\Tracking.cs" "$PSScriptRoot\src\DailyReports.cs" "$PSScriptRoot\tests\Tests.cs" "$PSScriptRoot\tests\TrackingTests.cs" "$PSScriptRoot\tests\DeadlineTests.cs" "$PSScriptRoot\tests\DailyReportTests.cs" "$PSScriptRoot\tests\DeadlineSchedulingTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Biên dịch kiểm thử thất bại.' }
Write-Output "Đã tạo $OutputName và verification\Tests.exe."
