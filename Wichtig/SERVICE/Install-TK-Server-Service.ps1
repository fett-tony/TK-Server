param(
    [string]$ExePath = "",
    [string]$ServiceName = "TK-Server",
    [string]$DisplayName = "TK-Server",
    [string]$Description = "TK-Server SIP/GSM/SVC Runtime Service"
)

if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $projectRoot = Resolve-Path (Join-Path $scriptRoot "..\..")
    $candidates = Get-ChildItem -Path $projectRoot -Filter "TK-Server.exe" -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending

    if ($candidates.Count -eq 0) {
        throw "TK-Server.exe wurde nicht gefunden. Bitte -ExePath explizit angeben."
    }

    $ExePath = $candidates[0].FullName
}

if (-not (Test-Path $ExePath)) {
    throw "EXE nicht gefunden: $ExePath"
}

$binPath = '"{0}" -startsvc' -f $ExePath

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -ne $existing) {
    sc.exe stop $ServiceName | Out-Null
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

sc.exe create $ServiceName binPath= $binPath start= auto DisplayName= $DisplayName | Out-Null
sc.exe description $ServiceName $Description | Out-Null
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null

Write-Host "Dienst installiert: $ServiceName"
Write-Host "Binary: $binPath"
