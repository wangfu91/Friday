using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            player.CurrentPalyingFile = selectedFile;
            await player.PlayCommand.Execute();
            await Task.Delay(5000).ContinueWith(async _ =>
            {
                await player.StopCommand.Execute();
            });
            Assert.True(string.IsNullOrEmpty(player.Diagnostics));
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
