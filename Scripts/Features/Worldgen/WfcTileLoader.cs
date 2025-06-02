using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using Godot;

public static class WfcTileLoader
    {
        // Load all WfcTile resources from a directory
        public static WfcTileSet LoadFromDirectory(string directoryPath)
        {
            var tileSet = new WfcTileSet();
            var tiles = new Godot.Collections.Array<WfcTile>();
            
            if (!DirAccess.DirExistsAbsolute(directoryPath))
            {
                GD.PrintErr($"WfcTileLoader: Directory not found: {directoryPath}");
                return tileSet;
            }
            
            LoadTilesRecursive(directoryPath, tiles);
            
            tileSet.Tiles = tiles;
            
            // Validate the loaded tile set
            var issues = tileSet.ValidateTileSet();
            if (issues.Count > 0)
            {
                GD.PrintErr($"WfcTileLoader: Validation issues found:");
                foreach (var issue in issues)
                {
                    GD.PrintErr($"  - {issue}");
                }
            }
            
            GD.Print($"WfcTileLoader: Loaded {tiles.Count} tiles from {directoryPath}");
            return tileSet;
        }
        
        private static void LoadTilesRecursive(string directoryPath, Godot.Collections.Array<WfcTile> tiles)
        {
            using var dir = DirAccess.Open(directoryPath);
            if (dir == null)
            {
                GD.PrintErr($"WfcTileLoader: Failed to open directory: {directoryPath}");
                return;
            }
            
            dir.ListDirBegin();
            var fileName = dir.GetNext();
            
            while (!string.IsNullOrEmpty(fileName))
            {
                var fullPath = directoryPath + "/" + fileName;
                
                if (dir.CurrentIsDir() && !fileName.StartsWith("."))
                {
                    // Recursively load from subdirectories
                    LoadTilesRecursive(fullPath, tiles);
                }
                else if (fileName.EndsWith(".tres") || fileName.EndsWith(".res"))
                {
                    // Try to load as WfcTile resource
                    var resource = GD.Load(fullPath);
                    if (resource is WfcTile tile)
                    {
                        tiles.Add(tile);
                        GD.Print($"WfcTileLoader: Loaded tile '{tile.TileName}' from {fullPath}");
                    }
                }
                
                fileName = dir.GetNext();
            }
            
            dir.ListDirEnd();
        }
    }
