param([string]$ProjectDir, [string]$OutDir);

$SolutionDir = "$($ProjectDir)..\"

$a = Get-Content "$($SolutionDir)changelog.md" | Select-String -Pattern "#\s*(\d+\.\d+\.\d+)"
$version = $a.Matches[0].Groups[1]

# Move tag to the latest commit
if (git tag -l $version) { git tag -d $version }
git tag $version
git push --tags -f

Remove-Item "$OutDir" -Recurse

#dotnet publish -r win-x86 /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true --self-contained true -o "$($OutDir)win-x86"
#dotnet publish -r win-arm /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true --self-contained true -o "$($OutDir)win-arm"

dotnet publish -c Release -r win-x64 /p:PublishAot=true /p:PublishReadyToRun=true /p:IncludeNativeLibrariesForSelfExtract=true /p:DebugType=None /p:DebugSymbols=false /p:CopyOutputSymbolsToPublishDirectory=false --self-contained true -o "$($OutDir)win-x64"
dotnet publish -c Release -r osx-x64 /p:PublishSingleFile=true /p:PublishTrimmed=true /p:IncludeNativeLibrariesForSelfExtract=true /p:DebugType=None /p:DebugSymbols=false /p:CopyOutputSymbolsToPublishDirectory=false --self-contained true -o "$($OutDir)osx-x64"

# Create Archive
Compress-Archive "$($OutDir)win-x64\*" "$($SolutionDir)AzgaarToCK3_$($version)_win-x64.zip" -force
Compress-Archive "$($OutDir)osx-x64\*" "$($SolutionDir)AzgaarToCK3_$($version)_osx-x64.zip" -force