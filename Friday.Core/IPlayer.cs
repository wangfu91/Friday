using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Friday.Core
{
    public interface IPlayer
    {
        bool CanPlay { get; }

        bool CanPause { get; }

        bool CanStop { get; }

        bool IsMuted { get; set; }

        double Volume { get; set; }

        double ChannelPosition { get; set; }

        double ChannelLength { get; }

        bool IsPlaying { get; set; }

        ICommand PlayCommand { get; set; }

        ICommand PauseCommand { get; set; }

        void Stop();

        void Pause();

        void Play();
    }
}
