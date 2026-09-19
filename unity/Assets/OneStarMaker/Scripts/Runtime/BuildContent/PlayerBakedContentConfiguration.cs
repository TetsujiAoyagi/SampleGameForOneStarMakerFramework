#nullable enable
using OneStarMaker.Foundation.Config;
namespace OneStarMaker.Runtime.BuildContent
{
    internal sealed class PlayerBakedContentConfiguration
    {
        internal PlayerBakedContentConfiguration(AppConfig config)
        { SchemaVersion=config.GetInt("content:schemaVersion",0);RuntimeMode=config.GetString("content:runtimeMode","");BuildIdentity=config.GetString("content:buildIdentity","");ContentSet=config.GetString("content:contentSet","");Target=config.GetString("content:target","");Representation=config.GetString("content:representation","");FirstScene=config.GetString("content:firstScene","");RelativeDirectory=config.GetString("content:relativeDirectory","");ProbeToken=config.GetString("content:probeToken",""); }
        internal int SchemaVersion{get;}internal string RuntimeMode{get;}internal string BuildIdentity{get;}internal string ContentSet{get;}internal string Target{get;}internal string Representation{get;}internal string FirstScene{get;}internal string RelativeDirectory{get;}internal string ProbeToken{get;}
    }
}
