namespace CadProcessorService.Helpers;

public static class FileHelper
{
    public static void EnsureDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    public static bool CheckFileHasCopied(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return false;

            using (File.OpenRead(filePath))
            {
                return true;
            }
        }
        catch
        {
            Thread.Sleep(5000);
            return CheckFileHasCopied(filePath);
        }
    }

    public static void MoveWithCreate(string source, string dest)
    {
        var dir = Path.GetDirectoryName(dest);
        if (!string.IsNullOrWhiteSpace(dir))
            EnsureDirectory(dir);

        if (File.Exists(dest))
            File.Delete(dest);

        File.Move(source, dest);
    }
}
