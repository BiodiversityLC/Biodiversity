cd ../
dotnet new tool-manifest
dotnet tool install --local evaisa.netcodepatcher.cli
cd Biodiversity
start /b del "install-netcode-patcher.cmd"
