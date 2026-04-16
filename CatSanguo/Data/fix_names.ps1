$content = Get-Content -Path "$PSScriptRoot\generals.json" -Raw -Encoding UTF8

$replacements = @{
    [char[]]@(0x732B,0x5B5F,0x5FB7) -join '' = [char[]]@(0x66F9,0x64CD) -join ''
    [char[]]@(0x732B,0x4EF2,0x8C0B) -join '' = [char[]]@(0x5B59,0x6743) -join ''
    [char[]]@(0x732B,0x5B50,0x9F99) -join '' = [char[]]@(0x8D75,0x4E91) -join ''
    [char[]]@(0x732B,0x4EF2,0x8FBE) -join '' = [char[]]@(0x53F8,0x9A6C,0x61FF) -join ''
    [char[]]@(0x732B,0x4EF2,0x9896) -join '' = [char[]]@(0x8463,0x5353) -join ''
    [char[]]@(0x732B,0x4F2F,0x7B26) -join '' = [char[]]@(0x5B59,0x7B56) -join ''
    [char[]]@(0x732B,0x516C,0x55E3) -join '' = [char[]]@(0x5218,0x7985) -join ''
    [char[]]@(0x732B,0x6C49,0x5347) -join '' = [char[]]@(0x9EC4,0x5FE0) -join ''
    [char[]]@(0x732B,0x6587,0x957F) -join '' = [char[]]@(0x9B4F,0x5EF6) -join ''
    [char[]]@(0x732B,0x5999,0x624D) -join '' = [char[]]@(0x590F,0x4FAF,0x6E0A) -join ''
    [char[]]@(0x732B,0x5B50,0x5B5D) -join '' = [char[]]@(0x66F9,0x4EC1) -join ''
}

foreach ($key in $replacements.Keys) {
    $content = $content.Replace($key, $replacements[$key])
}

$content | Set-Content -Path "$PSScriptRoot\generals.json" -Encoding UTF8 -NoNewline
Write-Host "Done"
