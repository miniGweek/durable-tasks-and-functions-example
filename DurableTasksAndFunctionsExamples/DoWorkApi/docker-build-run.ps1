$ScriptDirectory = $PSScriptRoot
$DockerBuildContext = Split-Path $ScriptDirectory -Parent;
docker build -f "$ScriptDirectory\Dockerfile" -t doworkapi:dev $DockerBuildContext && `
    docker run --rm -d -p 5000:80 --name doworkapi doworkapi:dev