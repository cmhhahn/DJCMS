namespace DJCMS10.Models
{
    public class Settings
    {
        public string MusicLibrary { get; set; }
        public string PinnedPlaylist { get; set; }
        public double Volume { get; set; }
        // Indicates this settings instance was created because no settings.json existed
        public bool IsNew { get; set; } = false;
        // List of saved playlist file paths (persisted to settings.json)
        public List<string> Playlists { get; set; } = new List<string>();
    }
}
