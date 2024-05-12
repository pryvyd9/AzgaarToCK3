# param([string]$SolutionDir, [string]$OutDir);
param([string]$ProjectDir, [string]$OutDir);

$SolutionDir = "$($ProjectDir)..\"

$a = Get-Content "$($SolutionDir)changelog.md" | Select-String -Pattern "#\s*(\d+\.\d+\.\d+)"
$version = $a.Matches[0].Groups[1]

# Move tag to the latest commit
if (git tag -l $version) { git tag -d $version }
git tag $version
git push --tags -f

Remove-Item "$OutDir" -Recurse

dotnet publish -r win-x86 /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true --self-contained true -o "$($OutDir)win-x86"
dotnet publish -r win-arm /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true --self-contained true -o "$($OutDir)win-arm"

# Create Archive
Compress-Archive "$($OutDir)win-x86\*" "$($SolutionDir)AzgaarToCK3_$($version)_win-x86.zip" -force
Compress-Archive "$($OutDir)win-arm\*" "$($SolutionDir)AzgaarToCK3_$($version)_win-arm.zip" -force