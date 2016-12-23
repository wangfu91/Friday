using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Friday.Visualization
{
    /// <summary>
    /// Provides access to functionality that is common
    /// across all sound players.
    /// </summary>
    public interface ISoundPlayer : INotifyPropertyChanged
    {
        /// <summary>
        /// Gets whether the sound player is currently playing audio.
        /// </summary>
        bool IsPlaying { get; }
    }
}
