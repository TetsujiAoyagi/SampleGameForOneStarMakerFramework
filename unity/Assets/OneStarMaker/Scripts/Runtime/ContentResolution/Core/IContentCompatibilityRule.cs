#nullable enable

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 候補を採ってよいかの述語。供給元の順序（policy）とも、候補があること（provider の回答）とも別の判定である。
    /// </summary>
    /// <remarks>
    /// この契約は既定の実装を置かない。identity 等値のような既定を置くと、互換判定（identity・digest・target・schema）の
    /// 製品実装が決めるべき既定値を先に凍結してしまうためである。製品実装が入るまでは、ホストは自前の規則を渡さない限り
    /// <see cref="ResolutionPolicy"/> を組めない。
    /// 規則が <see cref="System.OperationCanceledException"/> 以外の例外を投げると、resolver は fault として探索を止める（fail-closed）。
    /// </remarks>
    public interface IContentCompatibilityRule
    {
        /// <param name="reason">採らない場合の理由。採る場合も記録用に任意の文字列を返してよい。</param>
        bool Accepts(ContentRequest request, ContentCandidate candidate, out string reason);
    }
}
