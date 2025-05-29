Param(
    [string]$Output = ".\Card_Context.txt"
)


# 1. Gather tracked text files
#$extensions = '.cs','.csproj','.gox','.json','.xml','.yml','.mgcb','.spritefont','.gdshader','.md','.ps1','.godot','.tscn'
$extensions = '.cs','.json','.xml','.yml','.gdshader','.md','.tscn'

$files = git ls-files |
        Where-Object { $extensions -contains ([IO.Path]::GetExtension($_)) } |
        Where-Object { $_ -notmatch '(/\.vscode/|/\.idea/|addons/|README\.md|CLAUDE\.md|\.csproj$|\.sln$)' }
#        Where-Object { $_ -notmatch '(/\.vscode/|/\.idea/|test/|addons/|README\.md|CLAUDE\.md|\.csproj$|\.sln$)' }

# 2. Build hierarchical tree structure
function New-TreeNode($name) {
    [PSCustomObject]@{
        Name     = $name
        Children = [System.Collections.ArrayList]@()
    }
}

function Add-PathToTree($rootNode, $segments) {
    $current = $rootNode
    foreach ($seg in $segments) {
        $child = $current.Children | Where-Object { $_.Name -eq $seg } | Select-Object -First 1
        if (-not $child) {
            $child = New-TreeNode $seg
            $null = $current.Children.Add($child)
        }
        $current = $child
    }
}

function Write-TreeLines($node, $indent, [System.Collections.ArrayList]$lines) {
    $suffix = if ($node.Children.Count -gt 0) { '/' } else { '' }
    $lines.Add("$indent$($node.Name)$suffix") | Out-Null
    foreach ($child in $node.Children | Sort-Object Name) {
        Write-TreeLines $child ($indent + '-') $lines
    }
}

# Determine repo root (absolute) and initialize tree
$repoRoot = (& git rev-parse --show-toplevel).Trim()
$rootNode = New-TreeNode $repoRoot

# Insert each tracked file path into the tree
foreach ($file in $files) {
    $segs = $file -split '[\\/]'
    Add-PathToTree $rootNode $segs
}

# Emit tree lines
$treeLines = [System.Collections.ArrayList]@()
Write-TreeLines $rootNode '' $treeLines
$treeLines | Out-File -FilePath $Output -Encoding UTF8

# 3. Blank line before contents
Add-Content $Output ''

# 4. Append each file’s contents
foreach ($file in $files) {
    Add-Content $Output "=== Begin $file ==="
    if ([IO.Path]::GetExtension($file) -eq '.gox') {
        Add-Content $Output '[Contents of binary file omitted]'
    } else {
        Get-Content $file | Add-Content -Path $Output
    }
    Add-Content $Output "=== End $file ===`n"
}

Write-Host "Context dumped to $Output"

# 5. Copy the output file to the clipboard
Add-Type -AssemblyName System.Windows.Forms
$dropList = New-Object System.Collections.Specialized.StringCollection
$dropList.Add((Resolve-Path $Output).Path)
[System.Windows.Forms.Clipboard]::SetFileDropList($dropList)
Write-Host "Copied file [$Output] itself to the clipboard. You can now Paste it in Explorer."
