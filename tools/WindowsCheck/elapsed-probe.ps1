# Polls the game state block of a running Siedler1.exe and tallies the elapsed clock units per
# main-loop pass (state +0x284), grouped by the speed factor. Writes a summary to -Out every 5 s;
# the default lies outside the repository, because the summary holds values read from the original.
# Field offsets: docs/rules/program-index.md, "Game state block". Procedure: README.md, same folder.
param(
  [int]$Seconds = 900,
  [string]$Out = "$env:TEMP\elapsed-summary.txt"
)

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class Mem {
  [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(int access, bool inherit, int pid);
  [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out IntPtr read);
  public static byte[] Read(IntPtr h, long addr, int size) {
    var b = new byte[size]; IntPtr n;
    if (!ReadProcessMemory(h, new IntPtr(addr), b, size, out n)) return null;
    return b;
  }
}
"@

Write-Host "Waiting for Siedler1.exe ..."
while (-not ($p = Get-Process Siedler1 -ErrorAction SilentlyContinue | Select-Object -First 1)) { Start-Sleep -Milliseconds 500 }
$h = [Mem]::OpenProcess(0x0410, $false, $p.Id)   # QUERY_INFORMATION | VM_READ
if ($h -eq [IntPtr]::Zero) { throw "OpenProcess failed for pid $($p.Id); run the probe as the same user as the game." }
$delta = $p.MainModule.BaseAddress.ToInt64() - 0x400000
$ediBlock = 0x674000 + $delta
Write-Host ("pid {0}, image base 0x{1:x}" -f $p.Id, $p.MainModule.BaseAddress.ToInt64())

$hist = @{}      # speed -> elapsed -> count
$cycles = @{}    # speed -> "phasecount:total" -> count
$missed = @{}    # speed -> extra phase steps between two polls
$prevPhase = -1; $paused = 0; $failedReads = 0
$sw = [Diagnostics.Stopwatch]::StartNew(); $nextWrite = 5000

function Bump($table, $k1, $k2) {
  if (-not $table.ContainsKey($k1)) { $table[$k1] = @{} }
  $table[$k1][$k2] = 1 + [int]$table[$k1][$k2]
}

function Write-Summary {
  $lines = @("elapsed-probe  pid $($p.Id)  runtime $([int]($sw.ElapsedMilliseconds/1000)) s  paused passes $paused  failed reads $failedReads")
  foreach ($s in ($hist.Keys | Sort-Object)) {
    $lines += "speed $s"
    $lines += "  elapsed units per pass: " + (($hist[$s].Keys | Sort-Object | ForEach-Object { "$($_)=$($hist[$s][$_])" }) -join "  ")
    $lines += "  extra phase steps (several phases in one pass, or passes the poller missed): $([int]$missed[$s])"
    if ($cycles.ContainsKey($s)) {
      $lines += "  cycle totals (phase count:units): " + (($cycles[$s].Keys | Sort-Object | ForEach-Object { "$($_)=$($cycles[$s][$_])" }) -join "  ")
    }
  }
  Set-Content -Path $Out -Value $lines
}

while ($sw.ElapsedMilliseconds -lt $Seconds * 1000 -and -not $p.HasExited) {
  $ptr = [Mem]::Read($h, $ediBlock + 0x34, 4)
  $spd = [Mem]::Read($h, $ediBlock + 0x48, 1)
  if ($ptr -and $spd) {
    $state = [BitConverter]::ToUInt32($ptr, 0)
    $b = [Mem]::Read($h, $state, 0x290)
    if ($b -and ($b[0x1c8] -band 8)) {
      $phase = [BitConverter]::ToUInt16($b, 0x26c)
      $count = [BitConverter]::ToUInt16($b, 0x286)
      if ($prevPhase -ge 0 -and $phase -ne $prevPhase -and $count -gt 0) {
        $speed = $spd[0]
        $steps = ($phase - $prevPhase + $count) % $count
        $elapsed = [BitConverter]::ToUInt16($b, 0x284)
        if ([BitConverter]::ToInt32($b, 0x1fe) -eq 0) { $paused++ }
        elseif ($steps -eq 1) { Bump $hist $speed $elapsed }
        else { $missed[$speed] = [int]$missed[$speed] + $steps - 1 }
        if ($phase -lt $prevPhase) { Bump $cycles $speed ("{0}:{1}" -f $count, [BitConverter]::ToUInt16($b, 0x28a)) }
      }
      $prevPhase = $phase
    } else {
      if (-not $b) { $failedReads++ }
      $prevPhase = -1
    }
  } else { $failedReads++ }
  if ($sw.ElapsedMilliseconds -ge $nextWrite) { Write-Summary; $nextWrite += 5000 }
  $t = $sw.Elapsed.TotalMilliseconds + 0.5
  while ($sw.Elapsed.TotalMilliseconds -lt $t) { }
}
Write-Summary
Write-Host "Done. Summary in $Out"
