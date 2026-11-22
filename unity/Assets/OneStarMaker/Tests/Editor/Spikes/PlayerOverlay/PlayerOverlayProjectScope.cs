#nullable enable

using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using E = OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayExpected;

namespace OneStarMaker.Tests.Editor.Spikes.PlayerOverlay
{
    /// <summary>
    /// Player の build の間だけ変える 3 つの設定（scripting backend、managed stripping の水準、Addressables の「Player と一緒に build する」
    /// 設定）の記録・変更・復元（HANDOFF §3、I-3 (b)）。値は製品の PlayerBuildProjectMutation と同じ（IL2CPP、High、DoNotBuildWithPlayer）
    /// だが、製品の code は呼ばない。
    /// 3 つとも追跡している file の値なので、開始から復元までを、呼び手が yield・await を挟まない 1 つの同期の呼び出しの中で使う
    /// （domain reload や test の中断で Dispose が失われない）。復元の後に保存し、2 つの file を変える前の bytes に書き戻す（Unity の再
    /// serialize による、変えていない行の差分を残さない）。Unity が途中で終わった時は、実行担当が git status で検出して戻す（AC9）。
    /// test の asmdef は Addressables の Editor の assembly を参照しないので、Addressables の設定は reflection で読み書きする。
    /// </summary>
    internal sealed class PlayerOverlayProjectScope : IDisposable
    {
        public const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";
        private readonly ScriptingImplementation _backend;
        private readonly ManagedStrippingLevel _stripping;
        private readonly object _addressables;
        private readonly PropertyInfo _buildOption;
        private readonly object _buildOptionBefore;
        private readonly string _addressablesPath;
        private readonly byte[] _projectBytes, _addressablesBytes;
        private bool _disposed;

        public PlayerOverlayProjectScope()
        {
            var type = Type.GetType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor")
                       ?? throw new InvalidOperationException("Addressables editor assembly is not loaded.");
            _addressables = type.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                            ?? throw new InvalidOperationException("Addressables settings are missing.");
            _buildOption = _addressables.GetType().GetProperty("BuildAddressablesWithPlayerBuild", BindingFlags.Public | BindingFlags.Instance)
                           ?? throw new MissingMemberException("AddressableAssetSettings.BuildAddressablesWithPlayerBuild");
            _addressablesPath = AssetDatabase.GetAssetPath((UnityEngine.Object)_addressables);
            _projectBytes = File.ReadAllBytes(Disk(ProjectSettingsPath));
            _addressablesBytes = File.ReadAllBytes(Disk(_addressablesPath));
            _backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            _stripping = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone);
            _buildOptionBefore = _buildOption.GetValue(_addressables)!;
            Before = Describe();
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.High);
                _buildOption.SetValue(_addressables, Enum.Parse(_buildOption.PropertyType, "DoNotBuildWithPlayer"));
                During = Describe();
            }
            catch { Dispose(); throw; }
        }

        public string Before { get; }
        public string During { get; private set; } = "";
        public string After { get; private set; } = "";
        public string ProjectHashBefore => E.Sha256(_projectBytes);
        public string AddressablesHashBefore => E.Sha256(_addressablesBytes);
        public string ProjectHashAfter { get; private set; } = "";
        public string AddressablesHashAfter { get; private set; } = "";
        public string AddressablesPath => _addressablesPath;

        /// <summary>値が開始前と同じで、2 つの file の bytes が開始前と同じか（AC9 の (1)）。</summary>
        public bool Restored => After == Before && ProjectHashAfter == ProjectHashBefore && AddressablesHashAfter == AddressablesHashBefore;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, _backend);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, _stripping);
                _buildOption.SetValue(_addressables, _buildOptionBefore);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                // 値を戻して保存した後に、変える前の bytes を書き戻す。Addressables の設定は import し直して、memory の object を disk に揃える。
                File.WriteAllBytes(Disk(ProjectSettingsPath), _projectBytes);
                File.WriteAllBytes(Disk(_addressablesPath), _addressablesBytes);
                AssetDatabase.ImportAsset(_addressablesPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                After = Describe();
                ProjectHashAfter = E.Sha256(File.ReadAllBytes(Disk(ProjectSettingsPath)));
                AddressablesHashAfter = E.Sha256(File.ReadAllBytes(Disk(_addressablesPath)));
            }
        }

        private string Describe() =>
            "backend=" + PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone)
            + " stripping=" + PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone)
            + " addressablesWithPlayer=" + _buildOption.GetValue(_addressables);

        private static string Disk(string projectRelative) => Path.Combine(Path.GetDirectoryName(Application.dataPath)!, projectRelative);
    }
}
