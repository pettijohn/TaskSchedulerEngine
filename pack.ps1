$ErrorActionPreference = 'Stop'

# two digit year (%y), 2 day of year (%j), 2 minute of day
#$versionNum = get-date -UFormat %y.%V.%H.%M
$year = get-date -AsUTC -UFormat %y
$dayOfYear = (get-date -AsUTC -UFormat %j).TrimStart("0")
$hour = get-date -AsUTC -UFormat %H
$min = get-date -AsUTC -UFormat %M
$minOfDay = (([Int32]$hour) * 60 + ([Int32]$min)).ToSTring().TrimStart("0")
$versionNum = "${year}.${dayOfYear}.${minOfDay}"


$projPath = get-item ".\src\TaskSchedulerEngine.csproj"
$csproj = [xml] (get-content $projPath)
$csproj.Project.PropertyGroup.PackageVersion = $versionNum
$csProj.Save($projPath)

Read-Host -Prompt $versionNum;

$apiKey = get-content .nuget
dotnet pack -c Release -o .\out\
dotnet nuget push --source https://api.nuget.org/v3/index.json --api-key $apiKey ".\out\TaskSchedulerEngine.${versionNum}.nupkg"

#git tag $versionNum
#git commit -a -m "${versionNum}"
#git push
#git push --tags 