using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts;

public partial class TextureLoader : Node
{
    [Export] public string[] FolderPaths { get; set; } = {};

    public Dictionary<string,Texture2D[]> Textures { get; } = new();

    public override void _Ready()
    {
        LoadTextures();
    }

    private void LoadTextures()
    {
        for(int i = 0; i < FolderPaths.Length; i+=2)
        {
            var dir = DirAccess.Open(FolderPaths[i+1]);
            if (dir == null)
                return;

            dir.ListDirBegin();
            string fileName;
            List<Texture2D> textureList = new List<Texture2D>();
            while ((fileName = dir.GetNext()) != "")
            {
                if (dir.CurrentIsDir())
                    continue;

                var path = $"{FolderPaths[i+1]}{fileName}";
                if (!path.EndsWith(".png")) continue;
                var tex = ResourceLoader.Load<Texture2D>(path);
                if (tex != null)
                    textureList.Add(tex);
            }
            dir.ListDirEnd();
            Textures.Add(FolderPaths[i],textureList.ToArray());
        }
    }
}