using MediaPlayerWidget.Domain;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace SMTC2
{
    public class Controller : IController
    {
        private GlobalSystemMediaTransportControlsSessionManager _manager;
        private GlobalSystemMediaTransportControlsSession _session;

        public event EventHandler MediaChanged;
        public event EventHandler PlayStateChanged;

        private bool _isMediaLoaded = false;
        private bool _isPlaying = false;

        private string _artist;
        private string _title;
        private string _albumTitle;
        private ImageSource _albumCover;

        public void Initialize()
        {
            _ = InitializeAsync();
        }

        public TimeSpan GetDuration()
        {
            return _session?.GetTimelineProperties().EndTime ?? TimeSpan.Zero;
        }

        public TimeSpan GetPosition()
        {
            var props = _session?.GetTimelineProperties();
            if (props == null)
                return TimeSpan.Zero;

            // Since timeline properties are not updated immeditately and there is a delay
            // we calculate the ellapsed time since the last update and add it to the last position
            var ellapsed = (DateTime.UtcNow - props.LastUpdatedTime).TotalSeconds;

            return props.Position.Add(TimeSpan.FromSeconds(ellapsed));
        }

        public string GetSongAlbum()
        {
            return _albumTitle;
        }

        public string GetSongArtist()
        {
            return _artist;
        }

        public ImageSource GetSongCover()
        {
            return _albumCover;
        }

        public string GetSongTitle()
        {
            return _title;
        }

        public bool IsMediaLoaded()
        {
            return _isMediaLoaded;
        }

        public bool IsPlaying()
        {
            return _isPlaying;
        }

        public void Next()
        {
            _ = _session?.TrySkipNextAsync();
        }

        public void PlayPause()
        {
            _ = _session?.TryTogglePlayPauseAsync();
        }

        public void Previous()
        {
            if (GetPosition() > TimeSpan.Zero)
                SetPosition(TimeSpan.Zero);
            else
                _ = _session?.TrySkipPreviousAsync();
        }

        public void SetPosition(TimeSpan position)
        {
            _ = _session?.TryChangePlaybackPositionAsync((long)position.Ticks);
        }

        public void Shutdown()
        {
            if (_session != null)
            {
                _session.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
                _session.TimelinePropertiesChanged -= Session_TimelinePropertiesChanged;
                _session.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
            }

            if (_manager != null)
            {
                _manager.CurrentSessionChanged -= Manager_CurrentSessionChanged;
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                _manager.CurrentSessionChanged += Manager_CurrentSessionChanged;

                await AttachToCurrentSessionAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private async void Manager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            try
            {
                await AttachToCurrentSessionAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private async Task AttachToCurrentSessionAsync()
        {
            if (_manager == null)
                return;

            var newSession = _manager.GetCurrentSession();

            if (newSession == null)
            {
                _isMediaLoaded = false;
                return;
            }

            if (_session != null)
            {
                _session.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
                _session.TimelinePropertiesChanged -= Session_TimelinePropertiesChanged;
                _session.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
            }

            _session = newSession;

            _session.PlaybackInfoChanged += Session_PlaybackInfoChanged;
            _session.TimelinePropertiesChanged += Session_TimelinePropertiesChanged;
            _session.MediaPropertiesChanged += Session_MediaPropertiesChanged;

            var props = await newSession.TryGetMediaPropertiesAsync();
            var playbackInfo = newSession.GetPlaybackInfo();

            if (props == null)
            {
                _isMediaLoaded = false;
                return;
            }

            _isMediaLoaded = true;

            _artist = props.Artist;
            _title = props.Title;
            _albumTitle = props.AlbumTitle;

            if (props.Thumbnail != null)
            {
                _albumCover = await LoadImageAsync(props.Thumbnail);
            }
            else
            {
                _albumCover = null;
            }

            _isPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            MediaChanged?.Invoke(this, EventArgs.Empty);
            PlayStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private static async Task<ImageSource> LoadImageAsync(IRandomAccessStreamReference thumb)
        {
            var stream = await thumb.OpenReadAsync();
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = stream.AsStreamForRead();
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        private async void Session_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            var props = await sender.TryGetMediaPropertiesAsync();

            if (props == null)
            {
                _isMediaLoaded = false;
            }

            _isMediaLoaded = true;

            _artist = props.Artist;
            _title = props.Title;
            _albumTitle = props.AlbumTitle;

            if (props.Thumbnail != null)
            {
                _albumCover = await LoadImageAsync(props.Thumbnail);
            } else
            {
                _albumCover = null;
            }

            _isPlaying = sender.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            MediaChanged?.Invoke(this, EventArgs.Empty);
            PlayStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Session_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            var playbackInfo = sender.GetPlaybackInfo();

            _isPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            PlayStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Session_TimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
        {

        }
    }
}
