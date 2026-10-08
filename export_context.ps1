Param(
    [string]$Output = ".\Card_Context.txt",
    [switch]$Full
)


# 1. Gather tracked text files
# Default: the file tree only, over a wide set of extensions, with Tests/ included.
# -Full: a narrow set of extensions, Tests/ excluded, and each file's contents after the tree.
if ($Full) {
    $extensions = '.cs','.json','.xml','.yml','.gdshader','.md'
    $skipTests = 'Tests/|'
} else {
    $extensions = '.cs','.csproj','.gox','.json','.xml','.yml','.mgcb','.spritefont','.gdshader','.md','.godot','.tscn','.tres','.png'
    $skipTests = ''
}
$excluded = '(/\.vscode/|/\.idea/|' + $skipTests + 'addons/|README\.md|CLAUDE\.md|\.csproj$|\.sln$)'

$files = git ls-files |
        Where-Object { $extensions -contains ([IO.Path]::GetExtension($_)) } |
        Where-Object { $_ -notmatch $excluded }

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

# 4. With -Full, append the contents of each file
if ($Full) {
    foreach ($file in $files) {
        Add-Content $Output "=== Begin $file ==="
        if ([IO.Path]::GetExtension($file) -eq '.gox') {
            Add-Content $Output '[Contents of binary file omitted]'
        } else {
            Get-Content $file | Add-Content -Path $Output
        }
        Add-Content $Output "=== End $file ===`n"
    }
}

Write-Host "Context dumped to $Output"

# 5. Copy the output file to the clipboard
Add-Type -AssemblyName System.Windows.Forms
$dropList = New-Object System.Collections.Specialized.StringCollection
$dropList.Add((Resolve-Path $Output).Path)
[System.Windows.Forms.Clipboard]::SetFileDropList($dropList)
Write-Host "Copied file [$Output] itself to the clipboard. You can now Paste it in Explorer."
