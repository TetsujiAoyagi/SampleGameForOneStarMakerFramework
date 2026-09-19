#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace OneStarMaker.Runtime.BuildContent
{
    /// <summary>process 内だけで directory revision の登録、利用、削除を直列化する。</summary>
    public static class ContentRevisionGate
    {
        private static readonly object Sync = new();
        private static Registration? _registration;
        private static readonly List<Deletion> Deletions = new();
        private static readonly List<Registration> Reads = new();

        internal static IDisposable AcquireRead(string buildIdentity,string target,string absolutePath)
        {
            if(!TryNormalize(buildIdentity,target,absolutePath,out var path))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,"Content read lease input is invalid.",buildIdentity,target);
            lock(Sync)
            {
                if(Deletions.Exists(x=>SameRevisionOrPath(x.Identity,x.Target,x.Path,buildIdentity,target,path)))
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.DeletionInProgress,"A content deletion is in progress.",buildIdentity,target);
                RevisionProcessLease process;try{process=RevisionProcessLease.AcquireRead(buildIdentity,target,path);}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){throw new ContentDirectoryException(ContentDirectoryFailureCode.RevisionLockUnavailable,"Revision read lock is unavailable.",buildIdentity,target,innerException:ex);}var value=new Registration(buildIdentity,target,path);Reads.Add(value);
                return new CompositeLease(process,()=>{lock(Sync)Reads.Remove(value);});
            }
        }

        public static bool TryAcquireDelete(string buildIdentity, string target, string absolutePath,
            out ContentDeletionLease? lease, out ContentDirectoryFailureCode rejection)
        {
            lease = null;
            if (!TryNormalize(buildIdentity, target, absolutePath, out var path))
            { rejection = ContentDirectoryFailureCode.InvalidConfiguration; return false; }
            lock (Sync)
            {
                if (Deletions.Exists(value => SameRevisionOrPath(value.Identity, value.Target, value.Path,
                    buildIdentity, target, path)))
                { rejection = ContentDirectoryFailureCode.DeletionInProgress; return false; }
                if (_registration != null && SameRevisionOrPath(_registration.Identity, _registration.Target,
                    _registration.Path, buildIdentity, target, path))
                {
                    rejection = _registration.Path == path && (_registration.Identity != buildIdentity || _registration.Target != target)
                        ? ContentDirectoryFailureCode.PathInUse : ContentDirectoryFailureCode.RevisionBusy;
                    return false;
                }
                if(Reads.Exists(value=>SameRevisionOrPath(value.Identity,value.Target,value.Path,buildIdentity,target,path)))
                { rejection=ContentDirectoryFailureCode.RevisionBusy; return false; }
                RevisionProcessLease? process;
                try { if(!RevisionProcessLease.TryAcquireDelete(buildIdentity,target,path,out process)){rejection=ContentDirectoryFailureCode.RevisionBusy;return false;} }
                catch(ContentDirectoryException){rejection=ContentDirectoryFailureCode.RevisionLockUnavailable;return false;}
                var deletion = new Deletion(buildIdentity, target, path);
                Deletions.Add(deletion);
                lease = new ContentDeletionLease(() => { process!.Dispose(); ReleaseDeletion(deletion); });
                rejection = default;
                return true;
            }
        }

        internal static IDisposable Reserve(string buildIdentity, string target, string absolutePath)
        {
            if (!TryNormalize(buildIdentity, target, absolutePath, out var path))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration, "Content directory registration input is invalid.", buildIdentity, target);
            lock (Sync)
            {
                if (Deletions.Exists(value => SameRevisionOrPath(value.Identity, value.Target, value.Path,
                    buildIdentity, target, path)))
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.DeletionInProgress,
                        "A content directory deletion is in progress.", buildIdentity, target);
                if (_registration != null)
                {
                    var code = _registration.Path == path && (_registration.Identity != buildIdentity || _registration.Target != target)
                        ? ContentDirectoryFailureCode.PathInUse : ContentDirectoryFailureCode.RevisionBusy;
                    throw new ContentDirectoryException(code, "A content directory is already registered in this process.", buildIdentity, target);
                }
                var registration = new Registration(buildIdentity, target, path);
                RevisionProcessLease process;try{process=RevisionProcessLease.AcquireRead(buildIdentity,target,path);}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){throw new ContentDirectoryException(ContentDirectoryFailureCode.RevisionLockUnavailable,"Revision registration lock is unavailable.",buildIdentity,target,innerException:ex);}
                _registration = registration;
                return new CompositeLease(process,new RegistrationLease(registration).Dispose);
            }
        }

        internal static bool TryNormalize(string identity, string target, string path, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(identity) || string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                if (!Path.IsPathRooted(path)) return false;
                var full = Path.GetFullPath(path);
                var root = Path.GetPathRoot(full)!;
                // 同じ物理 directory の末尾 separator・Windows 大小文字 alias を一つの
                // revision path に畳む。root 自体の separator は落とさない。
                full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (full.Length < root.Length) full = root;
                normalized = Path.DirectorySeparatorChar == '\\' ? full.ToUpperInvariant() : full;
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            { return false; }
        }

        private static void ReleaseDeletion(Deletion deletion)
        {
            lock (Sync)
            {
                Deletions.Remove(deletion);
            }
        }

        // 単一の active session 制約は、無関係な revision の物理削除まで止める理由にならない。
        // identity または実 path が同じ場合だけ利用と削除を排他し、別 identity で同じ path を削る抜け道も塞ぐ。
        private static bool SameRevisionOrPath(string identity, string target, string path,
            string otherIdentity, string otherTarget, string otherPath)
            => (identity == otherIdentity && target == otherTarget) || path == otherPath;

        private sealed class Registration { internal Registration(string i, string t, string p) { Identity=i; Target=t; Path=p; } internal string Identity {get;} internal string Target {get;} internal string Path {get;} }
        internal sealed class Deletion { internal Deletion(string i, string t, string p) { Identity=i; Target=t; Path=p; } internal string Identity {get;} internal string Target {get;} internal string Path {get;} }
        private sealed class RegistrationLease : IDisposable { private Registration? _value; internal RegistrationLease(Registration value)=>_value=value; internal Registration? Value => _value; public void Dispose(){ lock(Sync){ if(_value != null && ReferenceEquals(_registration,_value)) _registration=null; _value=null; } } }
        private sealed class CompositeLease:IDisposable { private IDisposable? _inner;private Action? _release;internal CompositeLease(IDisposable inner,Action release){_inner=inner;_release=release;}public void Dispose(){var release=_release;_release=null;try{release?.Invoke();}finally{_inner?.Dispose();_inner=null;}} }
    }

    /// <summary>物理削除そのものは行わず、DIST が finally まで保持する process 内 delete lease。</summary>
    public sealed class ContentDeletionLease : IDisposable
    {
        private Action? _release;
        internal ContentDeletionLease(Action release) => _release = release;
        public void Dispose() { var release = _release; _release = null; release?.Invoke(); }
    }
}
