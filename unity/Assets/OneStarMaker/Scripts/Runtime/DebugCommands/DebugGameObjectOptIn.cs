#nullable enable

using System;

namespace OneStarMaker.Runtime.DebugCommands
{
    /// <summary>
    /// GameObject コマンドを1つの catalog へまとめて登録する。
    /// 既定の dispatcher は変えず、呼んだ catalog にだけコマンドが現れる。
    /// </summary>
    public static partial class DebugGameObjectCommands
    {
        /// <summary>
        /// list、select、set-active、local transform、Renderer の6コマンドだけを登録する。
        /// 登録中は世界を読まない。同じ catalog への二度目は重複として失敗する。
        /// </summary>
        public static void RegisterGameObjectCommands(
            DebugCommandCatalog catalog,
            IDebugGameObjectWorld world,
            DebugGameObjectSelection selection)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (selection == null)
            {
                throw new ArgumentNullException(nameof(selection));
            }

            RegisterListAndSelect(catalog, world, selection);
            RegisterSetActive(catalog, world, selection);
            RegisterTransform(catalog, world, selection);
            RegisterRenderer(catalog, world, selection);
        }

        /// <summary>
        /// 新しい catalog と selection を作り、6コマンドを登録して返す。
        /// 戻り値を App 寿命で保持し、必要なアプリだけが catalog を dispatcher へ渡す。
        /// </summary>
        public static DebugGameObjectCommandRegistration CreateRegistration(IDebugGameObjectWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            var selection = new DebugGameObjectSelection();
            var catalog = new DebugCommandCatalog();
            RegisterGameObjectCommands(catalog, world, selection);
            return new DebugGameObjectCommandRegistration(catalog, selection, world);
        }
    }

    /// <summary>
    /// 1つの world と1つの selection に束ねた GameObject コマンド。
    /// シーンオブジェクトは持たない。dispatcher の既定実装はここから差し替えない。
    /// </summary>
    public sealed class DebugGameObjectCommandRegistration
    {
        internal DebugGameObjectCommandRegistration(
            DebugCommandCatalog catalog,
            DebugGameObjectSelection selection,
            IDebugGameObjectWorld world)
        {
            Catalog = catalog;
            Selection = selection;
            World = world;
        }

        public DebugCommandCatalog Catalog { get; }

        public DebugGameObjectSelection Selection { get; }

        public IDebugGameObjectWorld World { get; }
    }
}
