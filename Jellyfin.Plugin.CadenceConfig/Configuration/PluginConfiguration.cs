using System;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Jellyfin.Plugin.CadenceConfig.Sync;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.CadenceConfig.Configuration
{
    /// <summary>
    /// Plugin configuration for CadenceConfig — the server-operator-set values every Cadence
    /// client (web + native iOS) fetches at sign-in, so no user has to configure URLs by hand.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
        /// </summary>
        public PluginConfiguration()
        {
            MarlinUrl = string.Empty;
            SignupUrl = string.Empty;
            CastReceiverAppId = string.Empty;
            LidarrUrl = string.Empty;
            LidarrApiKey = string.Empty;
            MusicGrabberUrl = string.Empty;
            MusicGrabberApiKey = string.Empty;
            SyncIntervalHours = 12;
            DeezerSubscriptions = Array.Empty<DeezerSubscription>();
            StorytellerUrl = string.Empty;
            StorytellerSyncIntervalMinutes = 5;
            StorytellerLinks = Array.Empty<StorytellerLink>();
            StorytellerAdminUsername = string.Empty;
            StorytellerAdminToken = string.Empty;
        }

        /// <summary>
        /// Gets or sets the marlin-search (Meilisearch) base URL for faster search. Handed to
        /// clients as a non-secret — marlin's /search is unauthenticated, so no token is needed.
        /// </summary>
        public string MarlinUrl { get; set; }

        /// <summary>
        /// Gets or sets an optional sign-up URL shown on the client's sign-in screen (e.g. an
        /// invite/registration page for this server's Jellyfin).
        /// </summary>
        public string SignupUrl { get; set; }

        /// <summary>
        /// Gets or sets an optional Google Cast receiver application id for the client's casting.
        /// </summary>
        public string CastReceiverAppId { get; set; }

        /// <summary>
        /// Gets or sets the Lidarr base URL (e.g. http://localhost:8686). When set with an API key,
        /// the plugin proxies the client's "request missing music" calls to Lidarr, injecting the
        /// key SERVER-SIDE so it never reaches the browser or the app.
        /// </summary>
        public string LidarrUrl { get; set; }

        /// <summary>
        /// Gets or sets the Lidarr API key. A WRITE credential — it is NEVER sent to clients; the
        /// plugin's Lidarr proxy attaches it to outbound requests inside the Jellyfin server only.
        /// </summary>
        public string LidarrApiKey { get; set; }

        /// <summary>
        /// Gets or sets the Music Grabber base URL (e.g. https://musicgrabber.jpc.io). When set with
        /// an API key, a Deezer import fetches each MISSING track as a single song from the grabber
        /// (server-side), tags it from the Deezer metadata, and adds it to the playlist.
        /// </summary>
        public string MusicGrabberUrl { get; set; }

        /// <summary>
        /// Gets or sets the Music Grabber API key. Sent only from the server to the grabber; unlike the
        /// client's copy it never has to ship in a browser bundle.
        /// </summary>
        public string MusicGrabberApiKey { get; set; }

        /// <summary>
        /// Gets or sets how often (in hours) the scheduled task re-syncs every Deezer subscription,
        /// adding newly-available library tracks to each mirrored Jellyfin playlist.
        /// </summary>
        public int SyncIntervalHours { get; set; }

        /// <summary>
        /// Gets or sets the persisted Deezer→Jellyfin playlist subscriptions the scheduled task keeps
        /// in sync. An array because Jellyfin serializes plugin configuration as XML, which round-trips
        /// arrays cleanly (CA1819 suppressed in .editorconfig with that justification).
        /// </summary>
        public DeezerSubscription[] DeezerSubscriptions { get; set; }

        /// <summary>
        /// Gets or sets the Storyteller base URL (e.g. http://192.168.7.211:8001). When set, the plugin
        /// bridges audiobook positions between Jellyfin and Storyteller (Whispersync-style) and serves
        /// the Cadence client the aligned text for read-along. Empty disables the whole feature.
        /// </summary>
        public string StorytellerUrl { get; set; }

        /// <summary>
        /// Gets or sets how often (in minutes) the scheduled task pulls Storyteller reading positions
        /// into Jellyfin's saved playback positions. Default 5.
        /// </summary>
        public int StorytellerSyncIntervalMinutes { get; set; }

        /// <summary>
        /// Gets or sets the per-user Storyteller links (Jellyfin user → Storyteller session token).
        /// Tokens are secrets: they are used only inside the server and are never sent to a client.
        /// An array because Jellyfin serializes plugin configuration as XML (CA1819 suppressed).
        /// </summary>
        public StorytellerLink[] StorytellerLinks { get; set; }

        /// <summary>
        /// Gets or sets the Storyteller admin account's username (display only). With an admin token
        /// present, Jellyfin users with no link get a Storyteller account created for them automatically.
        /// </summary>
        public string StorytellerAdminUsername { get; set; }

        /// <summary>
        /// Gets or sets the Storyteller admin bearer token used to create accounts. A server-side
        /// secret: never returned by any endpoint and never rendered in the dashboard.
        /// </summary>
        public string StorytellerAdminToken { get; set; }
    }
}
