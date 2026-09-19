#nullable enable
using System;using System.Collections.Generic;
namespace SampleGame.DependOnAll.Editor.Build
{
    internal static class ContentDeliveryPlayBridge
    {
        private static readonly string[] Keys={"RUNTIMEMODE","INSTALLEDREVISIONPATH","MANIFESTSHA256","BUILDIDENTITY","REPRESENTATION"};private static readonly Dictionary<string,string?> Previous=new();private static readonly Dictionary<string,string> Installed=new();
        internal static void Apply(string root,string digest,string identity,string contentSet,string representation)
        {var values=new[]{"directory",root,digest,identity,representation};for(var i=0;i<Keys.Length;i++){var key="SAMPLEGAME_CONTENT__"+Keys[i];if(!Previous.ContainsKey(key))Previous[key]=Environment.GetEnvironmentVariable(key);Environment.SetEnvironmentVariable(key,values[i]);Installed[key]=values[i];}}
        internal static void Reset(){foreach(var value in Installed)if(Environment.GetEnvironmentVariable(value.Key)==value.Value)Environment.SetEnvironmentVariable(value.Key,Previous[value.Key]);Installed.Clear();Previous.Clear();}
    }
}
