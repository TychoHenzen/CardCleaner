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
const [{ checkReachability }, { strip }, { lineIndex }, { buildConfig }] =
  await Promise.all([
    import(moduleAt("lib/rules-reachability.mjs")),
    import(moduleAt("lib/strip.mjs")),
    import(moduleAt("lib/offsets.mjs")),
    import(moduleAt("lib/config.mjs")),
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
`;
const godotFile = fileAt("Scripts/GodotRegistrations.cs", godotSource);
const godotViolations = reachabilityFor([godotFile]);
const godotNames = godotViolations.map((violation) => violation.message.split(" ")[0]);
assert.deepEqual(godotNames.sort(), [
  "CommentAttributeOnly",
  "OrdinaryUnused",
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

process.stdout.write("quality-guard-godot: reachability fixtures passed\n");
