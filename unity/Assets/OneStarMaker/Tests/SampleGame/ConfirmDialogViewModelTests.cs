#nullable enable

using NUnit.Framework;
using OneStarMaker.Runtime.UISystem.Mvvm;
using SampleGame.OutGame.ConfirmDialog;
using UnityEngine.UIElements;

namespace OneStarMaker.Tests.SampleGame
{
    /// <summary>
    /// ViewModel が VisualElement を保持せずに binding できることを検証する。
    ///
    /// <para>
    /// ViewModel 側が UI 要素を掴むと、View の寿命に ViewModel が引きずられる。
    /// 表示状態は値として持ち、View 側が反映する、という向きを固定する。
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class ConfirmDialogViewModelTests
    {
        [Test]
        public void PresentationStableState_BindsMessageAndVisibilityWithoutVisualElementInViewModel()
        {
            var viewModel = new ConfirmDialogViewModel();
            var label = new Label();

            using var messageBinding = label.BindText(viewModel.Message);
            using var visibleBinding = label.BindVisible(viewModel.IsMessageVisible);

            viewModel.SetMessage("回復しますか？");
            viewModel.SetMessageVisible(false);

            Assert.That(label.text, Is.EqualTo("回復しますか？"));
            Assert.That(label.style.display.value, Is.EqualTo(DisplayStyle.None));

            viewModel.SetMessageVisible(true);

            Assert.That(label.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            viewModel.Dispose();
        }
    }
}
