#Requires -Version 7

<#
.SYNOPSIS
    The "agent is driving" strip HandsOn.psm1 shows across the top of the screen. Show-AgentBanner
    starts it; run it directly only to look at it.

.DESCRIPTION
    Topmost, click-through and never activated, so it takes no input and no focus from ES or an
    emulator. It writes its window handle to -StateFile, so Save-Screenshot can hide it for a
    full-screen capture, and exits once -Heartbeat has not been touched for -ExpireSec: the kit
    touches it on every key, so the strip goes away by itself when the agent stops driving.
#>
param(
    [Parameter(Mandatory)] [string] $Heartbeat,
    [Parameter(Mandatory)] [string] $StateFile,
    [int] $ExpireSec = 180
)

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
if (-not ('HandsOn.BannerForm' -as [type])) {
    Add-Type -ReferencedAssemblies System.Windows.Forms, System.Drawing, System.ComponentModel.Primitives, System.Windows.Forms.Primitives -TypeDefinition @'
namespace HandsOn {
    public class BannerForm : System.Windows.Forms.Form {
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override System.Windows.Forms.CreateParams CreateParams {
            get {
                var p = base.CreateParams;
                // WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE
                p.ExStyle |= 0x8 | 0x80 | 0x80000 | 0x20 | 0x8000000;
                return p;
            }
        }
    }
}
'@
}

$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$form = [HandsOn.BannerForm]::new()
$form.FormBorderStyle = 'None'
$form.ShowInTaskbar = $false
$form.TopMost = $true
$form.StartPosition = 'Manual'
$form.BackColor = [System.Drawing.Color]::FromArgb(176, 32, 32)
$form.Opacity = 0.85
$form.Bounds = [System.Drawing.Rectangle]::new($screen.Left + [int](($screen.Width - 720) / 2), $screen.Top, 720, 36)

$label = [System.Windows.Forms.Label]::new()
$label.Dock = 'Fill'
$label.TextAlign = 'MiddleCenter'
$label.ForeColor = [System.Drawing.Color]::White
$label.Font = [System.Drawing.Font]::new('Segoe UI', 12, [System.Drawing.FontStyle]::Bold)
$label.Text = 'RomMBat agent is driving this session: hands off the keyboard and pad'
$form.Controls.Add($label)

$timer = [System.Windows.Forms.Timer]::new()
$timer.Interval = 1000
$timer.add_Tick({
        $age = try { ((Get-Date) - (Get-Item -LiteralPath $Heartbeat -ErrorAction Stop).LastWriteTime).TotalSeconds } catch { [double]::MaxValue }
        if ($age -gt $ExpireSec) { $form.Close() }
    })
$form.add_Shown({
        Set-Content -LiteralPath $StateFile -Value "$PID $($form.Handle)" -NoNewline
        $timer.Start()
    })

try { [System.Windows.Forms.Application]::Run($form) }
finally { Remove-Item -LiteralPath $StateFile -ErrorAction SilentlyContinue }
