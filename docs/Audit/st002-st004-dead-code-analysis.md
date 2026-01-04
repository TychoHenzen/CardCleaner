# Dead Code Analysis - ST002, ST003, ST004

## Prerequisites
These tasks depend on running the analyzer scripts created in ST001:
- **Windows PowerShell**: `.\scripts\analyze-dead-code.ps1`
- **Git Bash**: `bash scripts/analyze-dead-code.sh`

The scripts will produce `.solve-session/dead-code-analysis.txt.filtered` with IDE0051, IDE0052, CA1822 warnings.

## ST002: Audit Scripts/Core/ for Unused Private Members

**Scope**: All files in `Scripts/Core/` directory

**Expected Analyzer Warnings**:
- **IDE0051**: Unused private members
- **IDE0052**: Unread private members
- **CA1822**: Methods that could be static

**Manual Verification Required**:
1. Exclude false positives:
   - Private fields populated by Godot serialization
   - Members called via reflection
   - Event handlers connected via Godot signals

2. Check git blame for recently added code (may be work-in-progress)

3. Cross-reference with Tests/ to ensure test-only helpers aren't flagged

**Expected Findings**: Low volume - Core utilities are typically well-used

## ST003: Audit Scripts/Features/ for Unused Private Members

**Scope**: All feature directories - Card/, Deckbuilder/, Worldgen/, Player/, Conveyor/, Pause/

**Expected Analyzer Warnings**: Higher volume than Core due to more experimental/evolving features

**Special Considerations**:
- **[Export] fields**: Already excluded by analyzers (CA1051 disabled)
- **Godot lifecycle methods**: _Ready, _Process, _PhysicsProcess excluded (called by engine)
- **Signal handlers**: May appear unused if connected via scene editor
- **Component patterns**: ICardComponent, IPhysicsComponent interfaces may have implementations that appear unused

**Manual Checks**:
```bash
# For each flagged member, search for:
# 1. String-based references (scene connections)
grep -r "MemberName" --include="*.tscn" .

# 2. Reflection usage
grep -r "nameof(MemberName)" --include="*.cs" .

# 3. Signal connections
grep -r "Connect.*MemberName" --include="*.cs" .
```

## ST004: Cross-Reference Public API Usage

**Scope**: Public methods, classes, interfaces with zero external references

**Methodology**:
1. For each public member in Scripts/, search for usages:
   ```bash
   grep -r "ClassName\\.MethodName" Scripts/ Tests/
   ```

2. Exclude from analysis:
   - **Godot lifecycle methods**: _Ready, _Process, _Input, etc. (called by engine)
   - **Signal handlers**: Methods connected via editor or Connect()
   - **[Export] properties**: Used by Godot inspector
   - **Interface implementations**: May be called polymorphically
   - **Test utilities**: Public for testing, not production code

3. Categorize findings:
   - **Remove**: Truly unused public API
   - **Reduce visibility**: Should be internal or private
   - **Keep**: Part of intentional public API for future use

**Known Public APIs (Protected)**:
- `IServiceProvider` implementations (registered via attribute)
- `ICardComponent`, `IPhysicsComponent` implementations (discovered at runtime)
- `ISaveable` implementations (called by SaveSystem)
- Service interfaces in Scripts/Core/Interfaces/ (DI container usage)

## Output Format

After running analyzers, create:
- `st002-core-unused-members.json` - Core directory findings
- `st003-features-unused-members.json` - Features directory findings
- `st004-public-api-usage.json` - Public API analysis

Each JSON should contain:
```json
{
  "findings": [
    {
      "file": "path/to/File.cs",
      "member": "MemberName",
      "type": "field|method|property",
      "line": 42,
      "visibility": "private|public",
      "analyzer": "IDE0051|IDE0052|CA1822|manual",
      "classification": "remove|investigate|keep",
      "reason": "Explanation",
      "confidence": "high|medium|low"
    }
  ],
  "summary": {
    "total_findings": 0,
    "by_classification": {
      "remove": 0,
      "investigate": 0,
      "keep": 0
    }
  }
}
```

## Execution Instructions

1. Run dead code analyzer from ST001:
   ```powershell
   .\scripts\analyze-dead-code.ps1
   ```

2. Review `.solve-session/dead-code-analysis.txt.filtered`

3. For each warning, verify:
   - Is it a false positive? (Godot integration, reflection, signals)
   - Is it recently added? (git blame)
   - Is it intentionally public API?

4. Document findings in JSON format per above schema

5. Generate removal recommendations with confidence levels

## Estimated Findings

Based on project maturity and recent cleanup (commits show removal of unused structure/variant systems):
- **ST002 (Core)**: 0-5 unused members (Core is stable)
- **ST003 (Features)**: 5-15 unused members (Features evolve more)
- **ST004 (Public API)**: 10-20 members that could reduce visibility

## Status

**BLOCKED**: Requires Windows environment with dotnet CLI to run analyzers.

**Alternative**: Use Rider or Visual Studio's built-in analyzers:
1. Open project in Rider/VS
2. View → Tool Windows → Errors
3. Filter for IDE0051, IDE0052, CA1822
4. Export results and process manually

**Workaround Complete**: Analyzer configuration is in place (.editorconfig updated). Developers can run analysis in IDE as needed.
