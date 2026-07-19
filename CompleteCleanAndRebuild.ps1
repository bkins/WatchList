<#
.SYNOPSIS
This script automates the process of cleaning and rebuilding a .NET solution. 
It can use the current folder and detect the *.sln file within that folder if 
SolutionPath and SolutionName are not provided.

.DESCRIPTION
This script deletes 'bin' and 'obj' folders, restores NuGet packages, and 
rebuilds the specified solution. If SolutionPath and SolutionName are not 
provided, it uses the current folder and detects the first *.sln file.

.PARAMETER SolutionPath
The path to the folder containing your solution. If not provided, the default 
path is the current folder. If neither SolutionPath nor SolutionName are 
provided, it will use the current folder.

.PARAMETER SolutionName
The name of your solution file. If not provided, the script detects the first 
*.sln file in the current folder. If neither SolutionPath nor SolutionName 
are provided, it will use the current folder and detect the first *.sln file.

.PARAMETER UseCurrentFolder
A switch to use the current folder and detect the *.sln file. If provided, it 
takes precedence over SolutionPath and SolutionName.

.PARAMETER BuildConfiguration
The build configuration (e.g., Debug or Release). If not provided, the 
default configuration is Debug.

.EXAMPLE
.\YourScript.ps1 -SolutionPath "Path\To\Your\Solution\Folder" -SolutionName "YourSolution.sln" -BuildConfiguration "Release"

.EXAMPLE
.\YourScript.ps1 -BuildConfiguration "Release"

.EXAMPLE
.\YourScript.ps1 -UseCurrentFolder
# Uses the current folder and detects the *.sln file.

.EXAMPLE
.\YourScript.ps1
# Uses the current folder and detects the *.sln file.

#>


param 
(
      [string] $SolutionPath
    , [string] $SolutionName
    , [switch] $UseCurrentFolder
    , [string] $BuildConfiguration = "Debug"
)

# Check if only one of SolutionPath or SolutionName is provided
if ($SolutionPath -xor $SolutionName) 
{
    $UseCurrentFolder = $true
}

# If -UseCurrentFolder is provided or UseCurrentFolder is true, 
# set SolutionPath and SolutionName based on the current folder
if ($UseCurrentFolder) 
{
    $SolutionPath = Get-Location
    $SolutionName = (Get-ChildItem -Path $SolutionPath -Filter *.sln | Select-Object -First 1).Name
}

Set-Location $SolutionPath

# Install the wasm-tools workload, if necessary
# (Only run workload restore if a MAUI or WASM project is detected)
if (Get-ChildItem -Recurse -Include *.csproj | Select-String -Pattern "maui" -Quiet) 
{
    dotnet workload restore
}

# Delete 'bin' and 'obj' folders
Get-ChildItem -Path .\ -Include bin,obj -Recurse | Remove-Item -Force -Recurse

# Restore NuGet packages
nuget restore $SolutionName

# Rebuild Solution using .NET CLI
dotnet build $SolutionName -c $BuildConfiguration
