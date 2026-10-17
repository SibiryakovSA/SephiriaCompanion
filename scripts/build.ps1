param(
    [Parameter(Mandatory = $true)][string]$GameDirectory,
    [string]$JavaHome = $env:JAVA_HOME,
    [string]$AndroidSdk,
    [string]$BuildToolsVersion = '34.0.0'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$portableDotnet = Join-Path $projectRoot 'tools\dotnet\dotnet.exe'
if (Test-Path -LiteralPath $portableDotnet) {
    $dotnet = $portableDotnet
    $env:DOTNET_ROOT = Split-Path -Parent $portableDotnet
} else {
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
}
if (!$JavaHome) {
    $javac = Get-Command javac -ErrorAction SilentlyContinue
    if ($javac) { $JavaHome = Split-Path -Parent (Split-Path -Parent $javac.Source) }
}
if (!$JavaHome -or !(Test-Path -LiteralPath (Join-Path $JavaHome 'bin\javac.exe'))) {
    throw 'JDK 17 is required. Set JAVA_HOME or pass -JavaHome.'
}
if (!$AndroidSdk) {
    $AndroidSdk = if ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT }
        elseif ($env:ANDROID_HOME) { $env:ANDROID_HOME }
        else { Join-Path $projectRoot 'tools\android' }
}
if (!(Test-Path -LiteralPath (Join-Path $GameDirectory 'Sephiria_Data\Managed\Assembly-CSharp.dll'))) {
    throw '-GameDirectory must point to the installed PC version of Sephiria.'
}
$buildTools = Join-Path $AndroidSdk ('build-tools\' + $BuildToolsVersion)
$androidJar = Join-Path $AndroidSdk 'platforms\android-34\android.jar'
foreach ($required in @($androidJar, (Join-Path $buildTools 'aapt.exe'), (Join-Path $buildTools 'd8.bat'),
    (Join-Path $buildTools 'zipalign.exe'), (Join-Path $buildTools 'apksigner.bat'))) {
    if (!(Test-Path -LiteralPath $required)) { throw "Missing Android SDK component: $required" }
}
& (Join-Path $PSScriptRoot 'generate-icon.ps1')
$env:DOTNET_CLI_HOME = Join-Path $projectRoot 'tools\dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:JAVA_HOME = $JavaHome
$env:PATH = "$JavaHome\bin;$env:PATH"
$artifacts = Join-Path $projectRoot 'artifacts'
$version = ([xml](Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'mod\SephiriaCompanion.csproj'))).Project.PropertyGroup.Version
$package = Join-Path $artifacts ('package-' + $version)
$buildId = [Guid]::NewGuid().ToString('N')
$staging = Join-Path $artifacts ('staging-' + $buildId)
$androidBuild = Join-Path $artifacts ('android-build\' + $buildId)
$classes = Join-Path $androidBuild 'classes'
$dex = Join-Path $androidBuild 'dex'
foreach ($directory in @($artifacts,$staging,$androidBuild,$classes,$dex)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
function Check-Exit([string]$step) { if ($LASTEXITCODE -ne 0) { throw "$step failed with exit code $LASTEXITCODE" } }
& $dotnet build (Join-Path $projectRoot 'mod\SephiriaCompanion.csproj') -c Release "-p:SephiriaGameDir=$GameDirectory" --nologo
Check-Exit 'Mod build'
$addon = Join-Path $staging 'AddOns\SephiriaCompanion'
New-Item -ItemType Directory -Path $addon -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'mod\bin\Release\netstandard2.1\SephiriaCompanion.dll') -Destination $addon -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'mod\metadata.json') -Destination $addon -Force
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'android\src') -Filter '*.java' -Recurse | Select-Object -ExpandProperty FullName)
& "$JavaHome\bin\javac.exe" -encoding UTF-8 --release 8 -classpath $androidJar -d $classes @sources
Check-Exit 'Java compilation'
$classesJar = Join-Path $androidBuild 'classes.jar'
& "$JavaHome\bin\jar.exe" cf $classesJar -C $classes .
Check-Exit 'Java archive'
& "$buildTools\d8.bat" --min-api 26 --lib $androidJar --output $dex $classesJar
Check-Exit 'Android bytecode'
$unsigned = Join-Path $androidBuild 'unsigned.apk'
& "$buildTools\aapt.exe" package -f -M (Join-Path $projectRoot 'android\AndroidManifest.xml') -S (Join-Path $projectRoot 'android\res') -I $androidJar -F $unsigned
Check-Exit 'APK resources'
Push-Location $dex
try { & "$buildTools\aapt.exe" add $unsigned 'classes.dex'; Check-Exit 'APK bytecode packaging' }
finally { Pop-Location }
$aligned = Join-Path $androidBuild 'aligned.apk'
& "$buildTools\zipalign.exe" -f 4 $unsigned $aligned
Check-Exit 'APK alignment'
$keyStore = Join-Path $projectRoot 'tools\companion-dev.keystore'
if (!(Test-Path -LiteralPath $keyStore)) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $keyStore) -Force | Out-Null
    & "$JavaHome\bin\keytool.exe" -genkeypair -keystore $keyStore -storepass android -keypass android -alias companion -keyalg RSA -keysize 2048 -validity 10000 -dname 'CN=Sephiria Companion Development'
    Check-Exit 'Development signing key'
}
$apk = Join-Path $artifacts ('SephiriaCompanion-' + $version + '.apk')
& "$buildTools\apksigner.bat" sign --ks $keyStore --ks-pass 'pass:android' --key-pass 'pass:android' --out $apk $aligned
Check-Exit 'APK signing'
& "$buildTools\apksigner.bat" verify $apk
Check-Exit 'APK verification'
Copy-Item -LiteralPath $apk -Destination $staging -Force
foreach ($readme in @('README.md', 'README.ru.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $readme) -Destination $staging -Force
}
$packageDocs = Join-Path $staging 'docs'
New-Item -ItemType Directory -Path $packageDocs -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\screenshots') -Destination $packageDocs -Recurse -Force
# Keep the previous package recoverable and never merge stale files into a release.
$previous = Join-Path $artifacts ('previous-' + $version + '-' + $buildId)
$artifactRoot = [IO.Path]::GetFullPath($artifacts) + [IO.Path]::DirectorySeparatorChar
foreach ($target in @($package, $previous, $staging)) {
    if (![IO.Path]::GetFullPath($target).StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Package path is outside artifacts: $target"
    }
}
if (Test-Path -LiteralPath $package) { Move-Item -LiteralPath $package -Destination $previous }
Move-Item -LiteralPath $staging -Destination $package
Compress-Archive -Path (Join-Path $package '*') -DestinationPath (Join-Path $artifacts ('SephiriaCompanion-' + $version + '.zip')) -Force
Get-FileHash -LiteralPath $apk,(Join-Path $artifacts ('SephiriaCompanion-' + $version + '.zip')) -Algorithm SHA256 | Format-Table Hash,Path
