using File = Google.Apis.Drive.v3.Data.File;

namespace linkybot;

public class DriveFileEntry
{
    public string Path;
    public File DriveFile;

    public DriveFileEntry(string path, File driveFile)
    {
        Path = path;
        DriveFile = driveFile;
    }
}