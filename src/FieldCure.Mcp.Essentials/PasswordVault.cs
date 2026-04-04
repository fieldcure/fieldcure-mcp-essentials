using System.Runtime.InteropServices;
using System.Text;

namespace FieldCure.Mcp.Essentials;

/// <summary>
/// Reads credentials from Windows Credential Manager (PasswordVault-compatible).
/// Uses CredRead P/Invoke with TargetName format "{resource}/{userName}".
/// </summary>
static class PasswordVault
{
    const int CredTypeGeneric = 1;

    /// <summary>
    /// Reads a credential value, or null if not found or not on Windows.
    /// </summary>
    public static string? Read(string resourceName, string userName = "default")
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        var targetName = $"{resourceName}/{userName}";

        if (!CredRead(targetName, CredTypeGeneric, 0, out var credPtr))
            return null;

        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
            if (cred.CredentialBlobSize == 0 || cred.CredentialBlob == IntPtr.Zero)
                return null;

            var bytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, bytes, 0, (int)cred.CredentialBlobSize);
            return Encoding.Unicode.GetString(bytes);
        }
        finally
        {
            CredFree(credPtr);
        }
    }

#pragma warning disable SYSLIB1054
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CredRead(string targetName, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern void CredFree(IntPtr buffer);
#pragma warning restore SYSLIB1054

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct CREDENTIAL
    {
        public uint Flags;
        public int Type;
        public string TargetName;
        public string Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }
}
