#nullable enable

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    /// <summary>
    /// Windows IL2CPP は DriveInfo.DriveFormat の icall を持たない。
    /// 公開契約の local NTFS 判定は kernel32 の volume information で行う。
    /// </summary>
    internal static class ContentLocalNtfs
    {
        private const uint DriveRemote = 4;

        internal static void Require(string absolutePath, string message)
        {
            var root = Path.GetPathRoot(Path.GetFullPath(absolutePath));
            if (string.IsNullOrEmpty(root))
                throw new ContentDeliveryException(ContentDeliveryFailureCode.CompatibilityMismatch, message);
            if (GetDriveType(root) == DriveRemote)
                throw new ContentDeliveryException(ContentDeliveryFailureCode.CompatibilityMismatch, message);
            var format = new StringBuilder(64);
            if (!GetVolumeInformation(root, null, 0, out _, out _, out _, format, format.Capacity)
                || !string.Equals(format.ToString(), "NTFS", StringComparison.OrdinalIgnoreCase))
                throw new ContentDeliveryException(ContentDeliveryFailureCode.CompatibilityMismatch, message);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetDriveType(string lpRootPathName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetVolumeInformation(
            string lpRootPathName,
            StringBuilder? lpVolumeNameBuffer,
            int nVolumeNameSize,
            out uint lpVolumeSerialNumber,
            out uint lpMaximumComponentLength,
            out uint lpFileSystemFlags,
            StringBuilder lpFileSystemNameBuffer,
            int nFileSystemNameSize);
    }
}
