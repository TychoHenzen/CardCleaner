Param(
    [string]$Output = ".\Card_Context.txt"
)

# 1. Gather tracked text files
$extensions = '.cs','.csproj','.gox','.json','.xml','.yml','.mgcb','.spritefont','.gdshader','.md','.ps1','.godot','.tscn'

$files = git ls-files |
        Where-Object { $extensions -contains ([IO.Path]::GetExtension($_)) } |
        Where-Object { $_ -notmatch '(/\.vscode/|/\.idea/|addons/|README\.md|CLAUDE\.md|\.csproj$|\.sln$)' }

# 2. Prompt for additional files (via <request> tag contents)
$input = Read-Host "Enter files or patterns to include (newline- or comma-separated, leave blank to show tree only)"

if ([string]::IsNullOrWhiteSpace($input)) {
    # No input: show tree only
    $showTree = $true
} else {
    # Input given: split by newlines or commas, expand wildcards
    $patterns = $input -split '[,\r?\n\s]+' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    $matched = @()
    foreach ($pat in $patterns) {
        $matched += Get-ChildItem -Path $pat -File -Recurse -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }
    }
    $files = $matched | Sort-Object -Unique
    $showTree = $false
}

# 3. Build hierarchical tree functions
function New-TreeNode($name) { [PSCustomObject]@{ Name=$name; Children=[System.Collections.ArrayList]@() } }
function Add-PathToTree($root, $segs) {
    $cur = $root
    foreach ($seg in $segs) {
        $child = $cur.Children | Where-Object Name -EQ $seg | Select-Object -First 1
        if (-not $child) { $child = New-TreeNode $seg; $null = $cur.Children.Add($child) }
        $cur = $child
    }
}
function Write-TreeLines($node, $indent, $lines) {
    $suffix = if ($node.Children.Count) {'/'} else {''}
    $lines.Add("$indent$($node.Name)$suffix") | Out-Null
    foreach ($c in $node.Children | Sort-Object Name) { Write-TreeLines $c ($indent+'-') $lines }
}

# Determine repo root
$repoRoot = (& git rev-parse --show-toplevel).Trim()

if ($showTree) {
    # Build and output tree
    $rootNode = New-TreeNode $repoRoot
    foreach ($f in $files) {
        $segs = $f -split '[\\/]'
        Add-PathToTree $rootNode $segs
    }
    $treeLines = [System.Collections.ArrayList]@()
    Write-TreeLines $rootNode '' $treeLines
    $treeLines | Out-File -FilePath $Output -Encoding UTF8
    Write-Host "Tree dumped to $Output"
} else {
    # Write selected files to output
    # Clear or create output file
    Out-File -FilePath $Output -Encoding UTF8 -Force
    foreach ($file in $files) {
        Add-Content $Output "=== Begin $file ==="
        if ([IO.Path]::GetExtension($file) -eq '.gox') {
            Add-Content $Output '[Contents of binary file omitted]'
        } else {
            Get-Content $file | Add-Content -Path $Output
        }
        Add-Content $Output "=== End $file ===`n"
    }
    Write-Host "Contents dumped to $Output"
}

# Copy the output file to the clipboard
Add-Type -AssemblyName System.Windows.Forms
$dropList = New-Object System.Collections.Specialized.StringCollection
$dropList.Add((Resolve-Path $Output).Path)
[System.Windows.Forms.Clipboard]::SetFileDropList($dropList)
Write-Host "Copied file [$Output] to the clipboard. You can now paste it in Explorer."