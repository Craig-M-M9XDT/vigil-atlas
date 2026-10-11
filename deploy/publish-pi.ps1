# Builds a self-contained linux-arm64 package for a 64-bit Raspberry Pi OS.
# Usage: .\deploy\publish-pi.ps1 [-Pi pi@raspberrypi.local]
param([string]$Pi)
$out = Join-Path $PSScriptRoot '..\publish-pi'
dotnet publish "$PSScriptRoot\..\VigilAtlas\VigilAtlas.csproj" -c Release -r linux-arm64 --self-contained -o $out
if ($Pi) {
  scp -r "$out\*" "${Pi}:/tmp/vigil-atlas/"
  scp "$PSScriptRoot\vigil-atlas.service" "${Pi}:/tmp/"
  Write-Host "On the Pi: sudo mkdir -p /opt/vigil-atlas && sudo cp -r /tmp/vigil-atlas/* /opt/vigil-atlas/ && sudo chmod +x /opt/vigil-atlas/VigilAtlas"
  Write-Host "           sudo cp /tmp/vigil-atlas.service /etc/systemd/system/ && sudo systemctl enable --now vigil-atlas"
}
