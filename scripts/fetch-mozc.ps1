# Downloads mozc-ffi's Windows build (Mozc in one DLL, with its dictionary) into the Unity project.
# The DLL isn't in git; run this once after cloning, and again after changing the version.
#   powershell -ExecutionPolicy Bypass -File scripts\fetch-mozc.ps1
$ErrorActionPreference = 'Stop'
$version = '0.1.0'
$sha256 = '5afb7314f979140537539653b6a8b5d7c37400244e46d1b3b490ecd8d182434b'
$name = "mozc-ffi-$version-windows-x64"
$url = "https://github.com/Malorn44/mozc-ffi/releases/download/v$version/$name.zip"
$target = Join-Path $PSScriptRoot '..\Assets\Plugins\MozcFfi\x86_64'
$temp = Join-Path ([IO.Path]::GetTempPath()) "$name-$PID"

New-Item -ItemType Directory -Force $temp, $target | Out-Null
try {
    $zip = Join-Path $temp "$name.zip"
    Invoke-WebRequest $url -OutFile $zip -UseBasicParsing
    $actual = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
    if ($actual -ne $sha256) { throw "SHA-256 mismatch for $name.zip: $actual" }
    Expand-Archive $zip -DestinationPath $temp -Force
    Copy-Item (Join-Path $temp "$name\bin\mozc_ffi.dll") $target -Force
    Write-Host "mozc_ffi.dll $version -> $target"
} finally {
    Remove-Item $temp -Recurse -Force
}
