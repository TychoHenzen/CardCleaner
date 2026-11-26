@tool
extends EditorPlugin
## Registers the export filter, so every export of this project, whatever its preset, runs ExportPolicy.

const ExportFilter := preload("res://addons/export_guard/export_filter.gd")

var _export_filter: EditorExportPlugin


func _enter_tree() -> void:
	_export_filter = ExportFilter.new()
	add_export_plugin(_export_filter)


func _exit_tree() -> void:
	remove_export_plugin(_export_filter)
	_export_filter = null
