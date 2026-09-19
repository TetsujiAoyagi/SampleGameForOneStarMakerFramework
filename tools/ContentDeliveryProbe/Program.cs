#nullable enable
using System.Net;
using OneStarMaker.Runtime.BuildContent;

if (args.Length >= 1 && args[0] == "serve")
{
    if (args.Length != 3) { Console.Error.WriteLine("usage: serve root http://127.0.0.1:port/"); return 2; }
    var root = Path.GetFullPath(args[1]);
    using var listener = new HttpListener();
    listener.Prefixes.Add(args[2]);
    listener.Start();
    Console.WriteLine("READY");
    using var stop = new CancellationTokenSource();
    _ = Task.Run(() => { if (Console.ReadLine() == "RELEASE") stop.Cancel(); });
    try
    {
        while (!stop.IsCancellationRequested)
        {
            var context = await listener.GetContextAsync().WaitAsync(stop.Token);
            var relative = Uri.UnescapeDataString(context.Request.Url!.AbsolutePath.TrimStart('/'));
            var path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            { context.Response.StatusCode = 404; context.Response.Close(); continue; }
            context.Response.StatusCode = 200;
            context.Response.ContentLength64 = new FileInfo(path).Length;
            await using var file = File.OpenRead(path);
            await file.CopyToAsync(context.Response.OutputStream, stop.Token);
            context.Response.Close();
        }
    }
    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    return 0;
}

if (args.Length != 4 || (args[0] != "read" && args[0] != "delete"))
{ Console.Error.WriteLine("usage: read|delete identity target path"); return 2; }
IDisposable? lease = null;
try
{
    if (args[0] == "read") lease = RevisionProcessLease.AcquireRead(args[1], args[2], args[3]);
    else if (!RevisionProcessLease.TryAcquireDelete(args[1], args[2], args[3], out var deletion))
    { Console.WriteLine("BUSY"); return 3; }
    else lease = deletion;
    Console.WriteLine("READY");
    return Console.ReadLine() == "RELEASE" ? 0 : 4;
}
finally { lease?.Dispose(); }
