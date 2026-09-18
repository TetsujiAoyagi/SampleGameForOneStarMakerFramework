#nullable enable

using System;
using System.IO;

namespace OneStarMaker.Runtime.BuildContent
{
    /// <summary>process 内だけで directory revision の登録、利用、削除を直列化する。</summary>
    public static class ContentRevisionGate
    {
        private static readonly object Sync = new();
        private static Registration? _registration;
        private static Deletion? _deletion;

        public static bool TryAcquireDelete(string buildIdentity, string target, string absolutePath,
            out ContentDeletionLease? lease, out ContentDirectoryFailureCode rejection)
        {
            lease = null;
            if (!TryNormalize(buildIdentity, target, absolutePath, out var path))
            { rejection = ContentDirectoryFailureCode.InvalidConfiguration; return false; }
            lock (Sync)
            {
                if (_deletion != null)
                { rejection = _deletion.Path == path ? ContentDirectoryFailureCode.DeletionInProgress : ContentDirectoryFailureCode.RevisionBusy; return false; }
                if (_registration != null)
                {
                    rejection = _registration.Path == path && (_registration.Identity != buildIdentity || _registration.Target != target)
                        ? ContentDirectoryFailureCode.PathInUse : ContentDirectoryFailureCode.RevisionBusy;
                    return false;
                }
                var deletion = new Deletion(buildIdentity, target, path);
                _deletion = deletion;
                lease = new ContentDeletionLease(() => ReleaseDeletion(deletion));
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
                if (_deletion != null) throw new ContentDirectoryException(ContentDirectoryFailureCode.DeletionInProgress, "A content directory deletion is in progress.", buildIdentity, target);
                if (_registration != null)
                {
                    var code = _registration.Path == path && (_registration.Identity != buildIdentity || _registration.Target != target)
                        ? ContentDirectoryFailureCode.PathInUse : ContentDirectoryFailureCode.RevisionBusy;
                    throw new ContentDirectoryException(code, "A content directory is already registered in this process.", buildIdentity, target);
                }
                var registration = new Registration(buildIdentity, target, path);
                _registration = registration;
                return new RegistrationLease(registration);
            }
        }

        private static bool TryNormalize(string identity, string target, string path, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(identity) || string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) return false;
            try { normalized = Path.GetFullPath(path); return true; } catch (Exception) { return false; }
        }

        private static void ReleaseDeletion(Deletion deletion)
        {
            lock (Sync)
            {
                if (ReferenceEquals(_deletion, deletion)) _deletion = null;
            }
        }

        private sealed class Registration { internal Registration(string i, string t, string p) { Identity=i; Target=t; Path=p; } internal string Identity {get;} internal string Target {get;} internal string Path {get;} }
        internal sealed class Deletion { internal Deletion(string i, string t, string p) { Identity=i; Target=t; Path=p; } internal string Identity {get;} internal string Target {get;} internal string Path {get;} }
        private sealed class RegistrationLease : IDisposable { private Registration? _value; internal RegistrationLease(Registration value)=>_value=value; public void Dispose(){ lock(Sync){ if(_value != null && ReferenceEquals(_registration,_value)) _registration=null; _value=null; } } }
    }

    /// <summary>物理削除そのものは行わず、DIST が finally まで保持する process 内 delete lease。</summary>
    public sealed class ContentDeletionLease : IDisposable
    {
        private Action? _release;
        internal ContentDeletionLease(Action release) => _release = release;
        public void Dispose() { var release = _release; _release = null; release?.Invoke(); }
    }
}
