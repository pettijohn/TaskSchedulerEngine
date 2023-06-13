$ErrorActionPreference = 'Stop'

# two digit year, 2 digit weeknum, 2 digit hour, 2 digit mintu
#$versionNum = get-date -UFormat %y.%V.%H.%M
$hour = get-date -AsUTC -UFormat %H
$min = get-date -AsUTC -UFormat %M
$minOfDay = ([Int32]$hour) * 60 + ([Int32]$min)
$versionNum = (get-date -AsUTC -UFormat %y.%j) + "." + $minOfDay


$projPath = get-item ".\src\TaskSchedulerEngine.csproj"
$csproj = [xml] (get-content $projPath)
$csproj.Project.PropertyGroup.PackageVersion = $versionNum
$csProj.Save($projPath)

Read-Host -Prompt $versionNum;

$apiKey = get-content .nuget
dotnet pack -c Release -o .\out\
dotnet nuget push --source https://api.nuget.org/v3/index.json --api-key $apiKey ".\out\TaskSchedulerEngine.{$versionNum}.nupkg"

git tag $versionNum