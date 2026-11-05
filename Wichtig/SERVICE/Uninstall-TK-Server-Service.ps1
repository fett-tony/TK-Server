param(
    [string]$ServiceName = "TK-Server"
)

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $existing) {
    Write-Host "Dienst nicht vorhanden: $ServiceName"
    exit 0
}

if ($existing.Status -ne "Stopped") {
    sc.exe stop $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

sc.exe delete $ServiceName | Out-Null
Write-Host "Dienst entfernt: $ServiceName"
