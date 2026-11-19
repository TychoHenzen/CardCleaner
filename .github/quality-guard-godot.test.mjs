import assert from "node:assert/strict";
import { resolve } from "node:path";
import { pathToFileURL } from "node:url";

const scannerArgument = process.argv.find((arg) =>
  arg.startsWith("--scanner-root="),
);
if (!scannerArgument) throw new Error("--scanner-root=<scripts> is required");

const scannerRoot = resolve(
  process.cwd(),
  scannerArgument.slice("--scanner-root=".length),
);
const moduleAt = (relativePath) =>
  pathToFileURL(resolve(scannerRoot, relativePath)).href;
const [{ checkReachability }, { strip }, { lineIndex }, { buildConfig }, { checkDuplication }] =
  await Promise.all([
    import(moduleAt("lib/rules-reachability.mjs")),
    import(moduleAt("lib/strip.mjs")),
    import(moduleAt("lib/offsets.mjs")),
    import(moduleAt("lib/config.mjs")),
    import(moduleAt("lib/rules-duplicate.mjs")),
  ]);

function fileAt(rel, source, isTest = false) {
  return { rel, source, lang: "cs", isTest };
}

function scanAt(file) {
  const stripped = strip(file.source, file.lang);
  return { ...stripped, starts: lineIndex(stripped.code) };
}

function reachabilityFor(files) {
  return checkReachability({
    files,
    scans: new Map(files.map((file) => [file.rel, scanAt(file)])),
    config: buildConfig(),
  });
}

const godotSource = `using Godot;
[Tool]
[GlobalClass]
public partial class RegisteredTool : Resource {}
[Godot.GlobalClass]
public partial class QualifiedTool : Resource {}
[GlobalClassAttribute]
public partial class AttributeTool : Resource {}
[Godot.GlobalClassAttribute]
public partial class QualifiedAttributeTool : Resource {}
[Tool]
[GlobalClass]
[GlobalClassAttribute]
public partial class MultiAttributeTool : Resource {}
public class OrdinaryUnused {}
/*
[Godot.GlobalClass]
*/
public class CommentAttributeOnly {}
internal static class StringAttributeContainer
{
    private const string AttributeText = "[GlobalClass]";
}
public class StringAttributeOnly {}
[Service]
public class ServiceRegistered {}
[Service(typeof(IServiceThing))]
public class ServiceWithTypes {}
[ServiceAttribute]
public class ServiceAttributeRegistered {}
[Godot.Service]
public class QualifiedService {}
[ServiceX]
public class ServiceLookalike {}
`;
const godotFile = fileAt("Scripts/GodotRegistrations.cs", godotSource);
const godotViolations = reachabilityFor([godotFile]);
const godotNames = godotViolations.map((violation) => violation.message.split(" ")[0]);
assert.deepEqual(godotNames.sort(), [
  "CommentAttributeOnly",
  "OrdinaryUnused",
  "ServiceLookalike",
  "StringAttributeOnly",
].sort());
for (const violation of godotViolations)
  assert.equal(violation.rule, "dead-export");
assert.equal(
  godotViolations.find((violation) => violation.message.startsWith("OrdinaryUnused"))
    .severity,
  "high",
);
const godotScan = scanAt(godotFile);
assert.equal(godotScan.code.includes("[Godot.GlobalClass]"), true);
assert.equal(godotScan.code.includes('AttributeText = "'), false);
assert.equal(godotScan.code.includes("[Godot.GlobalClass]\n*/"), false);

const testOnlyFile = fileAt("Scripts/TestOnlyThing.cs", "public class TestOnlyThing {}\n");
const testReferenceFile = fileAt(
  "Tests/TestOnlyThingUsage.cs",
  "public class TestOnlyThingUsage { private TestOnlyThing _value = new(); }\n",
  true,
);
const testOnlyViolations = reachabilityFor([testOnlyFile, testReferenceFile]);
assert.equal(testOnlyViolations.length, 1);
assert.equal(testOnlyViolations[0].rule, "test-only-export");
assert.equal(testOnlyViolations[0].severity, "medium");

// C# using directives count as imports in the duplicate-block rule: three files that open with the same namespace
// list are not duplicated code, while three files that share a block of statements still are.
const sharedUsings = [
  "using System;",
  "using System.Collections.Generic;",
  "using static System.Math;",
  "global using System.Linq;",
  "using Vec = Godot.Vector3;",
  "using Godot;",
];
const sharedBody = [
  "var total = 0;",
  "using var stream = Open(path);",
  "total += stream.Length;",
  "Log(total);",
  "Flush(stream);",
  "return total;",
];
const duplicateFile = (rel, lines) => ({ rel, lines, lang: "cs", isTest: false });
const usingOnly = ["A", "B", "C"].map((name) =>
  duplicateFile(`Scripts/${name}.cs`, [...sharedUsings, `public class ${name} { }`]),
);
assert.deepEqual(checkDuplication(usingOnly, buildConfig()), []);

// The shared body includes a `using var` statement, which is code, not an import: blanking it would empty a line of
// every window and hide this duplicate.
const bodyShared = ["D", "E", "F"].map((name) =>
  duplicateFile(`Scripts/${name}.cs`, [`public class ${name}`, ...sharedBody]),
);
const bodyFindings = checkDuplication(bodyShared, buildConfig());
assert.equal(bodyFindings.length, 3);
for (const finding of bodyFindings) assert.equal(finding.rule, "duplicate-block");

process.stdout.write("quality-guard-godot: reachability and duplicate fixtures passed\n");
