extends RefCounted
## What an export does with each project file: KEEP it, SKIP it, or STUB it (ship the stand-in source instead).
## tools/export-check/export-check.mjs keeps the same folder list for the file table of an exported pck.

const KEEP := "keep"
const SKIP := "skip"
const STUB := "stub"

## Folders no exported game needs: test code and its fixtures, the in-engine test framework, the AI-editor helper
## and this plugin. A folder name ends with a slash, so a folder that only starts with the same letters is kept.
const SKIPPED_FOLDERS := [
	"res://Tests/",
	"res://UnitTests/",
	"res://addons/gdUnit4/",
	"res://addons/godot_ai/",
	"res://addons/export_guard/",
]

## project.godot lists the helper autoload in every build, so an export that only dropped its script would log an
## error on every start. The export ships this source in its place: an empty node.
const STUBS := {
	"res://addons/godot_ai/runtime/game_helper.gd": "extends Node\n",
}


func decide(path: String) -> String:
	if STUBS.has(path):
		return STUB
	for folder in SKIPPED_FOLDERS:
		if path.begins_with(folder):
			return SKIP
	return KEEP


func stub_source(path: String) -> String:
	return STUBS.get(path, "")
