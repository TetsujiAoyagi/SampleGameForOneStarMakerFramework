Set-StrictMode -Version Latest

# Filesystem metadata only. Role lists, manifests and invocation context belong
# to their domain owners; no resolver call creates a directory or changes ACLs.
if (-not ('OneStarMaker.Workflow.NativeStorePath' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace OneStarMaker.Workflow {
 public sealed class StorePathIdentity {
  public string physicalPath;
  public string volumeSerial;
  public string fileId;
  public bool isDirectory;
 }
 public static class NativeStorePath {
  [StructLayout(LayoutKind.Sequential)] struct BasicInfo {
   public uint attributes, creationLow, creationHigh, accessLow, accessHigh,
    writeLow, writeHigh, volumeSerial, sizeHigh, sizeLow, links, indexHigh, indexLow;
  }
  [StructLayout(LayoutKind.Sequential)] struct IdInfo {
   public ulong volume;
   [MarshalAs(UnmanagedType.ByValArray, SizeConst=16)] public byte[] id;
  }
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern SafeFileHandle CreateFileW(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
  [DllImport("kernel32.dll", SetLastError=true)]
  static extern bool GetFileInformationByHandle(SafeFileHandle handle,out BasicInfo info);
  [DllImport("kernel32.dll", SetLastError=true)]
  static extern bool GetFileInformationByHandleEx(SafeFileHandle handle,int kind,out IdInfo info,uint size);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle,StringBuilder path,uint size,uint flags);
  static SafeFileHandle Open(string path) {
   // OPEN_EXISTING, zero access and full sharing make this a metadata read.
   // Keep short KnownFolder aliases in their ordinary OS resolution context.
   // Only canonical local paths beyond MAX_PATH need extended Win32 notation;
   // neither caller-visible paths nor accepted input syntax changes.
   string nativePath=path.Length>=260 ? @"\\?\"+path : path;
   var handle=CreateFileW(nativePath,0,7,IntPtr.Zero,3,0x02200000,IntPtr.Zero);
   if(handle.IsInvalid){handle.Dispose();throw new IOException("windows-store-path-unavailable");}
   return handle;
  }
  static BasicInfo Basic(SafeFileHandle handle) {
   BasicInfo info;
   if(!GetFileInformationByHandle(handle,out info) || (info.attributes & 0x400)!=0)
    throw new IOException("windows-store-path-unavailable");
   return info;
  }
  static void CheckAncestors(string path) {
   string part=Path.GetPathRoot(path);
   foreach(var segment in path.Substring(part.Length).Split(new[]{'\\'},StringSplitOptions.RemoveEmptyEntries)) {
    part=Path.Combine(part,segment);
    using(var handle=Open(part)){Basic(handle);}
   }
  }
  public static StorePathIdentity Read(string path) {
   if(!OperatingSystem.IsWindows() || !Path.IsPathFullyQualified(path) ||
    path.StartsWith(@"\\") || path.IndexOf(':',2)>=0) throw new IOException("windows-store-path-unavailable");
   string full=Path.GetFullPath(path);
   CheckAncestors(full);
   using(var handle=Open(full)) {
    var basic=Basic(handle); IdInfo id;
    if(!GetFileInformationByHandleEx(handle,18,out id,(uint)Marshal.SizeOf<IdInfo>()))
     throw new IOException("windows-store-path-unavailable");
    var buffer=new StringBuilder(32768);
    uint length=GetFinalPathNameByHandleW(handle,buffer,(uint)buffer.Capacity,0);
    if(length==0 || length>=buffer.Capacity) throw new IOException("windows-store-path-unavailable");
    string physical=buffer.ToString();
    if(!physical.StartsWith(@"\\?\") || physical.StartsWith(@"\\?\UNC\",StringComparison.OrdinalIgnoreCase))
     throw new IOException("windows-store-path-unavailable");
    physical=physical.Substring(4);
    CheckAncestors(physical);
    return new StorePathIdentity {physicalPath=physical.TrimEnd('\\'),volumeSerial=basic.volumeSerial.ToString("x8"),
     fileId=Convert.ToHexString(id.id).ToLowerInvariant(),isDirectory=(basic.attributes & 0x10)!=0};
   }
  }
 }
}
'@
}
$script:IdentityHook=$null # Private offline metadata injection, never CLI/env input.
function Get-WindowsStorePathIdentity([string]$Path) {
    if($script:IdentityHook){return & $script:IdentityHook $Path}
    return [OneStarMaker.Workflow.NativeStorePath]::Read($Path)
}
function Assert-WindowsStorePathIdentity([string]$Path,$Expected) {
    $actual=Get-WindowsStorePathIdentity $Path
    $directory=if(($Expected -is [Collections.IDictionary] -and $Expected.Contains('isDirectory')) -or $Expected.PSObject.Properties['isDirectory']){[bool]$Expected.isDirectory}else{$true}
    if($actual.physicalPath -ine $Expected.physicalPath -or $actual.volumeSerial -cne $Expected.volumeSerial -or $actual.fileId -cne $Expected.fileId -or $actual.isDirectory -ne $directory){throw 'windows-store-identity-mismatch'}
    return $actual.physicalPath
}
Export-ModuleMember -Function Get-WindowsStorePathIdentity,Assert-WindowsStorePathIdentity
