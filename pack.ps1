# two digit year, 2 digit weeknum, 2 digit hour, 2 digit mintu
$versionNum = get-date -UFormat %y.%V.%H.%M
$projPath = ".\src\TaskSchedulerEngine.csproj"
$csproj = [xml] (get-content $projPath)
$csproj.Project.PropertyGroup.Version = $versionNum
$csProj.Save($projPath)

dotnet pack -c Release -o .\out\
# dotnet nuget push .\out\TaskSchedulerEngine.nupkg