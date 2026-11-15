namespace CryptSage.Core;

public static class DriveLetters
{
    public static char? FirstFree(IEnumerable<char>? used = null)
    {
        var source = used ?? DriveInfo.GetDrives().Select(d => d.Name[0]);
        var taken = new HashSet<char>(source.Select(char.ToUpperInvariant));

        for (var c = 'Z'; c >= 'D'; c--)
            if (!taken.Contains(c)) return c;
        return null;
    }
}
