#nullable enable
using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Security.Cryptography;using System.Text;
namespace OneStarMaker.Runtime.BuildContent
{
    internal sealed class RevisionProcessLease:IDisposable
    {
        private List<FileStream>? _streams;private RevisionProcessLease(List<FileStream> streams)=>_streams=streams;
        internal static RevisionProcessLease AcquireRead(string identity,string target,string path)=>Acquire(identity,target,path,false);
        internal static bool TryAcquireDelete(string identity,string target,string path,out RevisionProcessLease? lease){try{lease=Acquire(identity,target,path,true);return true;}catch(IOException){lease=null;return false;}catch(UnauthorizedAccessException ex){throw new ContentDirectoryException(ContentDirectoryFailureCode.RevisionLockUnavailable,"Revision lock is unavailable.",identity,target,innerException:ex);}}
        private static RevisionProcessLease Acquire(string identity,string target,string path,bool exclusive)
        { var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OneStarMaker","RevisionLocks","v1");Directory.CreateDirectory(root);var keys=new[]{Key(identity+"\0"+target),Key(Path.GetFullPath(path).ToUpperInvariant())}.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal);var opened=new List<FileStream>();try{foreach(var key in keys){var file=Path.Combine(root,key+".lock");EnsureFile(file);opened.Add(new FileStream(file,FileMode.Open,exclusive?FileAccess.ReadWrite:FileAccess.Read,exclusive?FileShare.None:FileShare.Read));}return new RevisionProcessLease(opened);}catch{foreach(var stream in opened)stream.Dispose();throw;} }
        private static void EnsureFile(string path){try{using var created=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.ReadWrite);}catch(IOException){if(!File.Exists(path))throw;}}
        private static string Key(string value){using var sha=SHA256.Create();return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(x=>x.ToString("x2")));}
        public void Dispose(){var streams=_streams;_streams=null;if(streams==null)return;foreach(var stream in streams)stream.Dispose();}
    }
}
