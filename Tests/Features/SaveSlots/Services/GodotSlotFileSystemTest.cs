using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.SaveSlots.Services;

[TestSuite]
[RequireGodotRuntime]
public class GodotSlotFileSystemTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void DeleteRemovesFileTempAndFolder()
    {
        var files = new GodotSlotFileSystem();
        // ASSUMPTION: a fresh GUID folder under user:// means cleanup touches only this test's files.
        var root = "user://save-slot-test-" + System.Guid.NewGuid().ToString("N");
        var file = root + "/save.json";
        var temp = root + "/save.json.tmp";
        try
        {
            files.EnsureFolder(root);
            files.WriteAllText(file, "x");
            files.WriteAllText(temp, "y");

            // The same order as SlotStore.Delete: temp, then file, then folder. Each call takes one checked path.
            files.Delete(temp);
            files.Delete(file);
            files.Delete(root);

            AssertThat(files.Exists(file)).IsFalse();
            AssertThat(files.Exists(temp)).IsFalse();
            AssertThat(DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(root))).IsFalse();
        }
        finally
        {
            files.Delete(temp);
            files.Delete(file);
            files.Delete(root);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void RenameOverExistingFileReplacesIt()
    {
        var files = new GodotSlotFileSystem();
        // ASSUMPTION: a fresh GUID folder under user:// means cleanup touches only this test's files.
        var root = "user://save-slot-test-" + System.Guid.NewGuid().ToString("N");
        var from = root + "/temp.json";
        var to = root + "/save.json";
        try
        {
            files.EnsureFolder(root);
            files.WriteAllText(to, "old");
            files.WriteAllText(from, "new");

            files.Rename(from, to);

            AssertThat(files.Exists(from)).IsFalse();
            AssertThat(files.ReadAllText(to)).IsEqual("new");
        }
        finally
        {
            files.Delete(to);
            files.Delete(from);
            files.Delete(root);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FailedRenameLeavesExistingTargetInPlace()
    {
        var files = new GodotSlotFileSystem();
        // ASSUMPTION: a fresh GUID folder under user:// means cleanup touches only this test's files.
        var root = "user://save-slot-test-" + System.Guid.NewGuid().ToString("N");
        var missing = root + "/temp.json";
        var to = root + "/save.json";
        try
        {
            files.EnsureFolder(root);
            files.WriteAllText(to, "old");

            var thrown = false;
            try
            {
                files.Rename(missing, to);
            }
            catch (System.IO.IOException)
            {
                thrown = true;
            }

            AssertThat(thrown).IsTrue();
            AssertThat(files.ReadAllText(to)).IsEqual("old");
        }
        finally
        {
            files.Delete(to);
            files.Delete(root);
        }
    }
}
