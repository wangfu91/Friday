using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Prism.Windows.Mvvm;

namespace Friday.Core.ViewModels
{
    public class ShellPageViewModel : ViewModelBase
    {
        private IAudioPlayer _player;

        public IAudioPlayer Player
        {
            get { return _player; }
            set { SetProperty(ref _player, value); }
        }

        public ShellPageViewModel()
        {
            Init();
        }

        private async void Init()
        {
            Player = new AudioGraphPlayer();
            //Player = new BassAudioPlayer();
            var selectedFile = await SelectPlaybackFile();
            Player.CurrentPlayingFile = selectedFile;
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
