Param(
    [string]$Output = ".\Card_Context.txt"
)

# 1. Gather tracked text files
$extensions = '.cs','.csproj','.gox','.json','.xml','.yml','.mgcb','.spritefont','.md','.ps1','.godot','.tscn'
$files = git ls-files |
        Where-Object { $extensions -contains ([IO.Path]::GetExtension($_)) } |
        Where-Object { $_ -notmatch '(/\.vscode/|/\.idea/|README\.md|CLAUDE\.md|\.csproj$|\.sln$)' }

# 2. Write a simple “tree” view
$files | ForEach-Object {
    $segments = $_ -split '[\\/]'
    for ($i = 0; $i -lt $segments.Length; $i++) {
        $indent = '-' * $i
        Write-Output ("{0}{1}" -f $indent, $segments[$i])
    }
} | Out-File -FilePath $Output -Encoding UTF8

# 3. Append file contents
Add-Content $Output ""
foreach ($file in $files) {
    Add-Content $Output "=== Begin $file ==="
    Get-Content $file | Add-Content -Path $Output
    Add-Content $Output "=== End $file ===`n"
}

Write-Host "Context dumped to $Output"


# load WinForms for Clipboard support
Add-Type -AssemblyName System.Windows.Forms

# create a StringCollection with the full path
$files = New-Object System.Collections.Specialized.StringCollection
$files.Add((Resolve-Path $Output).Path)

# put it on the clipboard as a FileDropList
[System.Windows.Forms.Clipboard]::SetFileDropList($files)

Write-Host "Copied file [$Output] itself to the clipboard. You can now Paste it in Explorer."