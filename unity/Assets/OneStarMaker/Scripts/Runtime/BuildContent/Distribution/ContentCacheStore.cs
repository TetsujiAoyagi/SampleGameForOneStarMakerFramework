#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    public enum ContentDeleteStatus { Deleted, DeferredBusy, DeleteFailed, Protected }
    public sealed class ContentDeleteResult { internal ContentDeleteResult(ContentDeleteStatus status,string key,string reason){Status=status;Key=key;Reason=reason;} public ContentDeleteStatus Status{get;} public string Key{get;} public string Reason{get;} }
    public sealed class ContentCacheSnapshot
    {
        internal ContentCacheSnapshot(IReadOnlyList<ContentCacheEntry> entries,IReadOnlyList<string> corruptPaths)
        { Entries=entries; CorruptPaths=corruptPaths; }
        public IReadOnlyList<ContentCacheEntry> Entries{get;}
        public IReadOnlyList<string> CorruptPaths{get;}
    }
    public sealed class ContentCacheEntry { internal ContentCacheEntry(string root,ContentInstallReceipt receipt,long bytes){RevisionRoot=root;ContentSet=receipt.contentSet;Revision=receipt.revision;Target=receipt.target;ManifestSha256=receipt.manifestSha256;Bytes=bytes;DateTime.TryParse(receipt.installedUtc,out var t);InstalledUtc=t;} public string RevisionRoot{get;} public string ContentSet{get;} public string Revision{get;} public string Target{get;} public string ManifestSha256{get;} public long Bytes{get;} public DateTime InstalledUtc{get;} }

    public sealed class ContentCacheStore
    {
        private readonly string _root;
        // store は cache root の値だけを保持し、永続 lock handle は持たない。
        // 各 public operation が transaction を取得し finally/using で返す。
        public ContentCacheStore(string cacheRoot){if(string.IsNullOrWhiteSpace(cacheRoot)||!Path.IsPathRooted(cacheRoot))throw new ArgumentException("Cache root must be absolute.",nameof(cacheRoot));_root=Path.GetFullPath(cacheRoot);}
        public string RootPath=>_root;
        internal string RevisionRoot(string set,string revision)=>Path.Combine(_root,"installed",set,revision);
        internal IDisposable AcquireTransaction()
        { var dir=Path.Combine(_root,"control");Directory.CreateDirectory(dir);try{return new FileStream(Path.Combine(dir,"transaction.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException ex){throw new ContentDeliveryException(ContentDeliveryFailureCode.Busy,"Content cache is busy.",ex);}catch(UnauthorizedAccessException ex){throw new ContentDeliveryException(ContentDeliveryFailureCode.LockUnavailable,"Content cache lock is unavailable.",ex);} }
        public ContentCacheSnapshot Inspect()
        // snapshot は観測結果であり、後続 delete の許可には使わない。削除直前には gate を再取得する。
        { using(AcquireTransaction()) return InspectUnsafe(); }
        internal ContentCacheSnapshot InspectUnsafe()
        {
            var list=new List<ContentCacheEntry>();
            var corrupt=new List<string>();
            var installed=Path.Combine(_root,"installed");
            if(!Directory.Exists(installed))return new ContentCacheSnapshot(list,corrupt);
            foreach(var receiptPath in Directory.GetFiles(installed,"receipt.json",SearchOption.AllDirectories))
            {
                try
                {
                    var receipt=ContentDeliveryFiles.ReadJson<ContentInstallReceipt>(receiptPath);
                    var root=Path.GetDirectoryName(receiptPath)!;
                    list.Add(new ContentCacheEntry(root,receipt,Directory.GetFiles(root,"*",SearchOption.AllDirectories).Sum(x=>new FileInfo(x).Length)));
                }
                catch(ContentDeliveryException) { corrupt.Add(receiptPath); }
            }
            return new ContentCacheSnapshot(list,corrupt);
        }
        public void MarkKnownGood(ContentInstallResult result)
        { using(AcquireTransaction()){var path=Path.Combine(_root,"control","known-good.json");var map=File.Exists(path)?ContentDeliveryFiles.ReadJson<KnownGoodFile>(path):new KnownGoodFile();
          var items=map.items.Where(x=>x.contentSet!=result.ContentSet).ToList();items.Add(new KnownGood{contentSet=result.ContentSet,revision=result.Revision,digest=result.ManifestSha256});map.items=items.ToArray();var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";ContentDeliveryFiles.WriteJson(temp,map);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path); } }
        public IReadOnlyList<ContentDeleteResult> Evict(long budgetBytes)
        // known-good と requested revision は候補から外し、利用中 revision は gate が拒否する。
        { using(AcquireTransaction()){var results=new List<ContentDeleteResult>();CleanupTombstones(results);var snapshot=InspectUnsafe();if(snapshot.CorruptPaths.Count>0)throw new ContentDeliveryException(ContentDeliveryFailureCode.InstallConflict,"A corrupt receipt prevents safe eviction.");var entries=snapshot.Entries;var used=MeasureManagedBytes();var pins=ReadPins();
          foreach(var entry in ContentEvictionPolicy.Select(entries,pins,used-budgetBytes)) { if(used<=budgetBytes)break; if(!ContentRevisionGate.TryAcquireDelete(entry.Revision,entry.Target,Path.Combine(entry.RevisionRoot,"content"),out var lease,out _)){results.Add(new ContentDeleteResult(ContentDeleteStatus.DeferredBusy,entry.Revision,"Revision is in use."));continue;}
            try { DeleteRevision(entry);used-=entry.Bytes;results.Add(new ContentDeleteResult(ContentDeleteStatus.Deleted,entry.Revision,"Deleted.")); } catch(Exception ex){results.Add(new ContentDeleteResult(ContentDeleteStatus.DeleteFailed,entry.Revision,ex.Message));} finally{lease!.Dispose();} }
          if(used>budgetBytes)throw new ContentDeliveryException(ContentDeliveryFailureCode.BudgetUnsatisfied,"Protected or busy revisions prevent the requested budget.",requiredBytes:used,availableBytes:budgetBytes);return results; } }
        internal void EnsureCapacityUnsafe(long requestedBytes,long budgetBytes,string requestedSet,string requestedRevision)
        {var snapshot=InspectUnsafe();if(snapshot.CorruptPaths.Count>0)throw new ContentDeliveryException(ContentDeliveryFailureCode.InstallConflict,"A corrupt receipt prevents safe admission.");var entries=snapshot.Entries;var used=MeasureManagedBytes();if(used+requestedBytes<=budgetBytes)return;var pins=ReadPins();pins.Add(requestedSet+"\n"+requestedRevision);
          foreach(var entry in ContentEvictionPolicy.Select(entries,pins,used+requestedBytes-budgetBytes)){if(used+requestedBytes<=budgetBytes)break;if(!ContentRevisionGate.TryAcquireDelete(entry.Revision,entry.Target,Path.Combine(entry.RevisionRoot,"content"),out var lease,out _))continue;try{DeleteRevision(entry);used-=entry.Bytes;}catch(Exception){}finally{lease!.Dispose();}}
          if(used+requestedBytes>budgetBytes)throw new ContentDeliveryException(ContentDeliveryFailureCode.BudgetUnsatisfied,"The cache cannot admit this revision without deleting protected or busy content.",requiredBytes:used+requestedBytes,availableBytes:budgetBytes);}
        private long MeasureManagedBytes()
        {
            long total=0;
            foreach(var name in new[]{"installed","staging","tombstone"})
            {
                var path=Path.Combine(_root,name);
                if(!Directory.Exists(path))continue;
                foreach(var file in Directory.GetFiles(path,"*",SearchOption.AllDirectories))
                    total=checked(total+new FileInfo(file).Length);
            }
            return total;
        }
        private void CleanupTombstones(List<ContentDeleteResult> results)
        // marker の元 identity/path で lease を再取得する。tombstone path を登録 authority に渡さない。
        {var root=Path.Combine(_root,"tombstone");if(!Directory.Exists(root))return;foreach(var directory in Directory.GetDirectories(root)){var markerPath=Path.Combine(directory,"marker.json");try{var marker=ContentDeliveryFiles.ReadJson<Tombstone>(markerPath);if(marker.version!=1||Path.GetFullPath(marker.cacheRoot)!=_root||!Path.GetFullPath(marker.revisionRoot).StartsWith(Path.Combine(_root,"installed")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Tombstone ownership marker is invalid.");if(!ContentRevisionGate.TryAcquireDelete(marker.revision,marker.target,marker.contentPath,out var lease,out _)){results.Add(new ContentDeleteResult(ContentDeleteStatus.DeferredBusy,marker.revision,"Tombstone cleanup is busy."));continue;}try{Directory.Delete(directory,true);results.Add(new ContentDeleteResult(ContentDeleteStatus.Deleted,marker.revision,"Tombstone cleanup completed."));}finally{lease!.Dispose();}}catch(Exception ex){results.Add(new ContentDeleteResult(ContentDeleteStatus.DeleteFailed,directory,ex.Message));}}}
        private HashSet<string> ReadPins(){var path=Path.Combine(_root,"control","known-good.json");return !File.Exists(path)?new HashSet<string>(StringComparer.Ordinal):ContentDeliveryFiles.ReadJson<KnownGoodFile>(path).items.Select(x=>x.contentSet+"\n"+x.revision).ToHashSet(StringComparer.Ordinal);}
        private void DeleteRevision(ContentCacheEntry entry)
        { var tombRoot=Path.Combine(_root,"tombstone",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(tombRoot);var marker=new Tombstone{version=1,cacheRoot=_root,revisionRoot=entry.RevisionRoot,contentPath=Path.Combine(entry.RevisionRoot,"content"),contentSet=entry.ContentSet,revision=entry.Revision,target=entry.Target,digest=entry.ManifestSha256};ContentDeliveryFiles.WriteJson(Path.Combine(tombRoot,"marker.json"),marker);var moved=Path.Combine(tombRoot,"revision");Directory.Move(entry.RevisionRoot,moved);Directory.Delete(tombRoot,true); }
        [Serializable] private sealed class KnownGoodFile{public KnownGood[] items=Array.Empty<KnownGood>();}[Serializable]private sealed class KnownGood{public string contentSet="";public string revision="";public string digest="";}[Serializable]private sealed class Tombstone{public int version;public string cacheRoot="";public string revisionRoot="";public string contentPath="";public string contentSet="";public string revision="";public string target="";public string digest="";}
    }
}
