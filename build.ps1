# DucKING'i derler ve artifacts klasörüne şunları üretir:
#   artifacts\DucKING-Setup-<sürüm>.exe   (kurulum dosyası)
#   artifacts\portable\DucKING.exe        (kurulumsuz tek dosya; -SkipPortable ile atlanır)
# Gerekenler: .NET 8 SDK ve Inno Setup 6.
param([switch]$SkipPortable)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'src\DucKING\DucKING.csproj'
$artifacts = Join-Path $root 'artifacts'

$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
Write-Host "DucKING $version derleniyor..."

if (Test-Path $artifacts) {
    # Açık (çalışan) bir exe silinemez ama yeniden adlandırılabilir; eski sürüm açıkken de derleme yapılabilsin.
    Get-ChildItem $artifacts -Recurse -File -Filter *.exe | ForEach-Object {
        $exe = $_  # catch içinde $_ hata kaydını gösterir
        try { Remove-Item $exe.FullName -Force -ErrorAction Stop }
        catch { Rename-Item $exe.FullName ("$($exe.BaseName)-eski-$(Get-Date -Format HHmmss).exe.old") }
    }
    Remove-Item $artifacts -Recurse -Force -ErrorAction SilentlyContinue
}

# Kurulum için klasör halinde yayın: tek dosyadaki gibi her açılışta Temp'e çıkarma yapılmaz.
dotnet publish $project -c Release -o (Join-Path $artifacts 'app') -nologo -v q `
    -p:PublishSingleFile=false -p:IncludeAllContentForSelfExtract=false -p:EnableCompressionInSingleFile=false
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish başarısız oldu.' }

$iscc = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 bulunamadı: winget install JRSoftware.InnoSetup' }

& $iscc /Q "/DMyAppVersion=$version" "/DSourceDir=$(Join-Path $artifacts 'app')" (Join-Path $root 'installer\DucKING.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup derlemesi başarısız oldu.' }

if (-not $SkipPortable) {
    dotnet publish $project -c Release -o (Join-Path $artifacts 'portable') -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Taşınabilir sürüm derlenemedi.' }
}

Get-ChildItem $artifacts -Filter *.exe -Recurse -Depth 1 |
    Where-Object { $_.DirectoryName -ne (Join-Path $artifacts 'app') } |
    ForEach-Object { '{0}  ({1:N0} MB)' -f $_.FullName, ($_.Length / 1MB) }
