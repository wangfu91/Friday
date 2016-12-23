using Friday.Visualization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;

namespace Friday.Core
{
    public interface IAudioPlayer : ISpectrumPlayer
    {
        TimeSpan Duration { get; set; }

        TimeSpan Position { get; set; }

        double PlaybackSpeed { get; set; }

        double Volume { get; set; }

        float CurrentVolumePeek { get; set; }

        ICommand PlayCommand { get; }

        ICommand PauseCommand { get; }

        IStorageFile CurrentPlayingFile { get; set; }
    }
}
