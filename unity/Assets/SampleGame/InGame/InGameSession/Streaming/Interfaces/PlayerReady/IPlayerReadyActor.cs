#nullable enable

using SampleGame.InGame.Player;
using UnityEngine;

namespace SampleGame.InGame.Streaming
{
    /// <summary>
    /// Teleport / 入力の書き込み面。物理所有は Player 側に残し、Sequence はこれを通すだけ。
    /// Unity GO 無しの単体テスト用に、読み取り専用の <see cref="IFlightReadModel"/> から分離する。
    /// </summary>
    internal interface IPlayerReadyActor : IFlightReadModel
    {
        void Teleport(Vector3 worldPosition, Vector3 lookForward);

        /// <summary>入力の書き込み。読み取りは <see cref="IFlightReadModel.InputEnabled"/>。</summary>
        new bool InputEnabled { get; set; }
    }
}
