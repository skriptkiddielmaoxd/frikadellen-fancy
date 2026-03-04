$exe = '.\installer\output\FrikadellenBAF_Setup_v3.0.0.exe'
if (-not (Test-Path $exe)) {
    Write-Output 'MISSING_INSTALLER'
    exit 0
}
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash
(Get-Content .\installer\RELEASE_NOTES_v3.0.0.md) -replace '\{\{SHA256\}\}', $hash | Set-Content .\installer\RELEASE_NOTES_v3.0.0.md
Write-Output "SHA256:$hash"
if (Get-Command gh -ErrorAction SilentlyContinue) { Write-Output 'GH_OK' } else { Write-Output 'NO_GH' }
if (git tag --list | Select-String '^v3\.0\.0$' -Quiet) { Write-Output 'TAG_EXISTS' } else { Write-Output 'NO_TAG' }
