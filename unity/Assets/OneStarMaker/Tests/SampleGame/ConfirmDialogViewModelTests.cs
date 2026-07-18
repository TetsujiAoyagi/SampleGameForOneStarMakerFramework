#nullable enable

using NUnit.Framework;
using OneStarMaker.Runtime.UISystem.Mvvm;
using SampleGame.OutGame.ConfirmDialog;
using UnityEngine.UIElements;

namespace OneStarMaker.Tests.SampleGame
{
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
