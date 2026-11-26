@tool
extends EditorExportPlugin
## Applies ExportPolicy to every file an export visits.

const ExportPolicy := preload("res://addons/export_guard/export_policy.gd")

var _policy := ExportPolicy.new()


func _get_name() -> String:
	return "ExportGuard"


func _export_file(path: String, _type: String, _features: PackedStringArray) -> void:
	var verdict := _policy.decide(path)
	if verdict == ExportPolicy.SKIP:
		skip()
	elif verdict == ExportPolicy.STUB:
		skip()
		add_file(path, _policy.stub_source(path).to_utf8_buffer(), false)
