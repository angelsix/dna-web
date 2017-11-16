# Get version number from config file
$version = Select-String -path ./dna.live.config '("version": ")(.*)",' -AllMatches | Foreach-Object {$_.Matches} | Foreach-Object { $_.Groups[2].Value }

Write-Host "Creating archive version $version"

Compress-Archive -Path Assets -DestinationPath "Releases/$version.zip"
Compress-Archive -Path Templates -DestinationPath "Releases/$version.zip" -Update
Compress-Archive -Path Variables -DestinationPath "Releases/$version.zip" -Update
Compress-Archive -Path dna.live.config -DestinationPath "Releases/$version.zip" -Update
Compress-Archive -Path readme.md -DestinationPath "Releases/$version.zip" -Update