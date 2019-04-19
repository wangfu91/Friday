using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Friday.Core;
using Xunit;

namespace Firday.Core.UnitTests
{
    public class AudioGraphPlayerTests
    {
        [Fact(DisplayName = "AudioGraphPlayer_PalybackTest")]
        public async Task AudioGraphPlayer_PalybackTest()
        {
            var selectedFile = await SelectPlaybackFile();

            var player = new AudioGraphPlayer();
            player.CurrentPlayingFile = selectedFile;
            player.PlayCommand.Execute(null);
            await Task.Delay(5000).ContinueWith(_ =>
            {
                player.StopCommand.Execute(null);
            });
            Assert.True(string.IsNullOrEmpty(player.DiagnosticsInfo));
        }

        private async Task<IStorageFile> SelectPlaybackFile()
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.MusicLibrary
            };
            picker.FileTypeFilter.Add(".mp3");
            picker.FileTypeFilter.Add(".aac");
            picker.FileTypeFilter.Add(".wav");

            var file = await picker.PickSingleFileAsync();
            return file;
        }
    }
}
