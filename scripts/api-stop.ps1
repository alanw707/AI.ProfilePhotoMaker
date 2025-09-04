param(
  [int[]] $Ports = @(5032, 7173)
)

Write-Host "Stopping API processes on ports: $($Ports -join ', ')"

foreach ($port in $Ports) {
  try {
    $conns = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
  } catch {
    $conns = @()
  }

  if ($conns) {
    $pids = $conns | Select-Object -ExpandProperty OwningProcess | Sort-Object -Unique
    foreach ($pid in $pids) {
      try {
        Write-Host "Killing PID $pid on port $port"
        Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
      } catch {
        Write-Warning "Failed to kill PID $pid"
      }
    }
  } else {
    Write-Host "No process found on port $port"
  }
}

Write-Host "API processes stopped (if any)."

